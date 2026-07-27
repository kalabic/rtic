using DotBase.Log;
using System.Threading.Channels;

namespace LibRTIC.Conversation.Control;

internal interface IRTICConversationCommandTransport
{
    Task RequestResponseAsync(
        RTICResponseRequest request,
        CancellationToken cancellationToken);

    Task CancelResponseAsync(
        string? responseId,
        CancellationToken cancellationToken);

    Task TruncateOutputAsync(
        RTICOutputCursor cursor,
        TimeSpan playedThrough,
        CancellationToken cancellationToken);
}

/// <summary>
/// Serializes outgoing commands and keeps the minimum receive-side state needed
/// to correlate interruption requests.
/// </summary>
internal sealed class ConversationCommandDispatcher : IDisposable
{
    private readonly object _gate = new();
    private readonly object _stateGate = new();
    private readonly InfoLog _info;
    private readonly Channel<QueuedCommand> _commands;
    private readonly CancellationTokenSource _forceCancellation;
    private readonly HashSet<string> _activeResponseIds = [];
    private readonly HashSet<OutputCursorKey> _knownOutputCursors = [];

    private IRTICConversationCommandTransport? _transport;
    private Task? _worker;
    private bool _acceptingCommands;
    private bool _shutdownRequested;
    private bool _disposed;

    public ConversationCommandDispatcher(
        InfoLog info,
        CancellationToken forceCancellation)
    {
        _info = info;
        _forceCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(forceCancellation);
        _commands = Channel.CreateUnbounded<QueuedCommand>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
    }

    public void Start(IRTICConversationCommandTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_transport is not null)
            {
                throw new InvalidOperationException(
                    "The conversation command dispatcher has already been started.");
            }
            if (_forceCancellation.IsCancellationRequested)
            {
                throw new OperationCanceledException(_forceCancellation.Token);
            }

            _transport = transport;
            _acceptingCommands = true;
            _worker = Task.Run(ProcessCommandsAsync);
        }
    }

    public Task RequestResponseAsync(
        RTICResponseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Enqueue(new RequestResponseCommand(request, cancellationToken));
    }

    public Task InterruptOutputAsync(
        RTICOutputInterruption request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Enqueue(new InterruptOutputCommand(request, cancellationToken));
    }

    public void Observe(RTICSessionEvent update)
    {
        ArgumentNullException.ThrowIfNull(update);

        lock (_stateGate)
        {
            switch (update)
            {
                case RTICResponseStarted started:
                    _activeResponseIds.Add(started.ResponseId);
                    break;
                case RTICResponseCompleted completed:
                    _activeResponseIds.Remove(completed.ResponseId);
                    break;
                case RTICOutputAudioDelta audio:
                    _knownOutputCursors.Add(OutputCursorKey.From(audio));
                    break;
                case RTICOutputAudioCompleted audio:
                    _knownOutputCursors.Add(OutputCursorKey.From(audio));
                    break;
                case RTICOutputContentPartStarted content
                    when content.Part is RTICAudioContentPart:
                    _knownOutputCursors.Add(OutputCursorKey.From(content));
                    break;
            }
        }
    }

    /// <summary>
    /// Stops accepting public commands and appends shutdown after every command that
    /// was accepted first. The callback changes receiver state at that serialized point.
    /// </summary>
    public void BeginShutdown(Action transitionReceiverState)
    {
        ArgumentNullException.ThrowIfNull(transitionReceiverState);

        lock (_gate)
        {
            if (_disposed || _shutdownRequested)
            {
                return;
            }

            _acceptingCommands = false;
            _shutdownRequested = true;
            if (_transport is null)
            {
                _commands.Writer.TryComplete();
                return;
            }

            if (!_commands.Writer.TryWrite(
                    new ShutdownCommand(transitionReceiverState)))
            {
                _info.Warning(
                    "The conversation command dispatcher could not enqueue shutdown.");
            }
            _commands.Writer.TryComplete();
        }
    }

    public void Dispose()
    {
        Task? worker;
        bool workerStopped = true;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _acceptingCommands = false;
            _commands.Writer.TryComplete();
            _forceCancellation.Cancel();
            worker = _worker;
        }

        if (worker is not null)
        {
            try
            {
                workerStopped = worker.Wait(TimeSpan.FromSeconds(5));
                if (!workerStopped)
                {
                    _info.Warning(
                        "The conversation command dispatcher did not stop within 5 seconds.");
                }
            }
            catch (AggregateException ex)
                when (ex.InnerExceptions.All(
                    static inner => inner is OperationCanceledException))
            {
            }
        }

        if (workerStopped)
        {
            _forceCancellation.Dispose();
        }
        else
        {
            _ = worker!.ContinueWith(
                static (_, state) => ((CancellationTokenSource)state!).Dispose(),
                _forceCancellation,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private Task Enqueue(QueuedCommand command)
    {
        if (command.CallerCancellation.IsCancellationRequested)
        {
            return Task.FromCanceled(command.CallerCancellation);
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_acceptingCommands || _transport is null)
            {
                throw new InvalidOperationException(
                    "The Realtime conversation is not accepting commands.");
            }
            if (!_commands.Writer.TryWrite(command))
            {
                throw new InvalidOperationException(
                    "The Realtime conversation command queue is closed.");
            }
        }

        command.RegisterCallerCancellation();
        return command.Task;
    }

    private async Task ProcessCommandsAsync()
    {
        try
        {
            await foreach (QueuedCommand command in
                _commands.Reader.ReadAllAsync(_forceCancellation.Token)
                    .ConfigureAwait(false))
            {
                if (!command.TryStart())
                {
                    command.ReleaseCallerCancellation();
                    continue;
                }

                try
                {
                    switch (command)
                    {
                        case RequestResponseCommand request:
                            await ExecuteRequestResponseAsync(request)
                                .ConfigureAwait(false);
                            break;
                        case InterruptOutputCommand interrupt:
                            await ExecuteInterruptOutputAsync(interrupt)
                                .ConfigureAwait(false);
                            break;
                        case ShutdownCommand shutdown:
                            await ExecuteShutdownAsync(shutdown)
                                .ConfigureAwait(false);
                            break;
                        default:
                            throw new InvalidOperationException(
                                $"Unsupported conversation command {command.GetType().Name}.");
                    }

                    command.Succeed();
                }
                catch (OperationCanceledException)
                    when (_forceCancellation.IsCancellationRequested)
                {
                    command.Cancel(_forceCancellation.Token);
                    throw;
                }
                catch (Exception ex)
                {
                    command.Fail(ex);
                }
            }
        }
        catch (OperationCanceledException)
            when (_forceCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_gate)
            {
                _acceptingCommands = false;
                _commands.Writer.TryComplete();
            }
            while (_commands.Reader.TryRead(out QueuedCommand? pending))
            {
                pending.Cancel(_forceCancellation.Token);
            }
        }
    }

    private async Task ExecuteRequestResponseAsync(
        RequestResponseCommand command)
    {
        IRTICConversationCommandTransport transport = GetTransport();
        try
        {
            await transport.RequestResponseAsync(
                    command.Request,
                    _forceCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (_forceCancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RTICConversationControlException(
                RTICConversationControlOperation.RequestResponse,
                "Failed to send the Realtime response request.",
                ex);
        }
    }

    private async Task ExecuteInterruptOutputAsync(
        InterruptOutputCommand command)
    {
        RTICOutputInterruption request = command.Request;
        if (!IsKnownOutputCursor(request.Cursor))
        {
            throw new InvalidOperationException(
                "The output interruption cursor was not observed in this conversation.");
        }

        IRTICConversationCommandTransport transport = GetTransport();
        List<Exception> failures = [];

        if (request.CancelResponseIfActive &&
            IsResponseActive(request.Cursor.ResponseId))
        {
            try
            {
                await transport.CancelResponseAsync(
                        request.Cursor.ResponseId,
                        _forceCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (_forceCancellation.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }

        try
        {
            await transport.TruncateOutputAsync(
                    request.Cursor,
                    request.PlayedThrough,
                    _forceCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (_forceCancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }

        if (failures.Count > 0)
        {
            Exception inner = failures.Count == 1
                ? failures[0]
                : new AggregateException(failures);
            throw new RTICConversationControlException(
                RTICConversationControlOperation.InterruptOutput,
                "Failed to send the complete Realtime output interruption.",
                inner);
        }
    }

    private async Task ExecuteShutdownAsync(ShutdownCommand command)
    {
        command.TransitionReceiverState();

        IRTICConversationCommandTransport transport = GetTransport();
        string[] activeResponseIds = GetActiveResponseIds();
        if (activeResponseIds.Length == 0)
        {
            await TryCancelForShutdownAsync(transport, null).ConfigureAwait(false);
            return;
        }

        foreach (string responseId in activeResponseIds)
        {
            await TryCancelForShutdownAsync(transport, responseId)
                .ConfigureAwait(false);
        }
    }

    private async Task TryCancelForShutdownAsync(
        IRTICConversationCommandTransport transport,
        string? responseId)
    {
        try
        {
            await transport.CancelResponseAsync(
                    responseId,
                    _forceCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (_forceCancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _info.Warning(
                "Failed to send a Realtime response cancellation during shutdown.",
                ex);
        }
    }

    private IRTICConversationCommandTransport GetTransport()
    {
        lock (_gate)
        {
            return _transport
                ?? throw new InvalidOperationException(
                    "The Realtime conversation command transport is unavailable.");
        }
    }

    private bool IsKnownOutputCursor(RTICOutputCursor cursor)
    {
        lock (_stateGate)
        {
            return _knownOutputCursors.Contains(OutputCursorKey.From(cursor));
        }
    }

    private bool IsResponseActive(string responseId)
    {
        lock (_stateGate)
        {
            return _activeResponseIds.Contains(responseId);
        }
    }

    private string[] GetActiveResponseIds()
    {
        lock (_stateGate)
        {
            return _activeResponseIds.ToArray();
        }
    }

    private abstract class QueuedCommand
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration _cancellationRegistration;
        private int _state;

        protected QueuedCommand(CancellationToken callerCancellation)
        {
            CallerCancellation = callerCancellation;
        }

        public CancellationToken CallerCancellation { get; }

        public Task Task => _completion.Task;

        public void RegisterCallerCancellation()
        {
            if (!CallerCancellation.CanBeCanceled)
            {
                return;
            }

            _cancellationRegistration = CallerCancellation.Register(
                static state => ((QueuedCommand)state!).CancelBeforeStart(),
                this);
            if (Volatile.Read(ref _state) != 0)
            {
                _cancellationRegistration.Dispose();
            }
        }

        public bool TryStart()
        {
            if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            {
                return false;
            }

            _cancellationRegistration.Dispose();
            return true;
        }

        public void Succeed()
        {
            Volatile.Write(ref _state, 3);
            _cancellationRegistration.Dispose();
            _completion.TrySetResult();
        }

        public void Fail(Exception exception)
        {
            Volatile.Write(ref _state, 3);
            _cancellationRegistration.Dispose();
            _completion.TrySetException(exception);
        }

        public void Cancel(CancellationToken cancellationToken)
        {
            Volatile.Write(ref _state, 3);
            _cancellationRegistration.Dispose();
            _completion.TrySetCanceled(cancellationToken);
        }

        public void ReleaseCallerCancellation()
            => _cancellationRegistration.Dispose();

        private void CancelBeforeStart()
        {
            if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
            {
                _completion.TrySetCanceled(CallerCancellation);
            }
        }
    }

    private sealed class RequestResponseCommand : QueuedCommand
    {
        public RequestResponseCommand(
            RTICResponseRequest request,
            CancellationToken callerCancellation)
            : base(callerCancellation)
        {
            Request = request;
        }

        public RTICResponseRequest Request { get; }
    }

    private sealed class InterruptOutputCommand : QueuedCommand
    {
        public InterruptOutputCommand(
            RTICOutputInterruption request,
            CancellationToken callerCancellation)
            : base(callerCancellation)
        {
            Request = request;
        }

        public RTICOutputInterruption Request { get; }
    }

    private sealed class ShutdownCommand : QueuedCommand
    {
        public ShutdownCommand(Action transitionReceiverState)
            : base(CancellationToken.None)
        {
            TransitionReceiverState = transitionReceiverState;
        }

        public Action TransitionReceiverState { get; }
    }

    private readonly record struct OutputCursorKey(
        string ResponseId,
        string ItemId,
        int OutputIndex,
        int ContentIndex)
    {
        public static OutputCursorKey From(IRTICContentEvent value)
            => new(
                value.ResponseId,
                value.ItemId,
                value.OutputIndex,
                value.ContentIndex);

        public static OutputCursorKey From(RTICOutputCursor value)
            => new(
                value.ResponseId,
                value.ItemId,
                value.OutputIndex,
                value.ContentIndex);
    }
}
