namespace LibRTIC.MiniTaskLib.Base;

internal sealed class ScheduledTask : FunctionTaskBase<Task>
{
    private const TaskCreationOptions ScheduleCreationOptions =
        TaskCreationOptions.DenyChildAttach |
        TaskCreationOptions.HideScheduler |
        TaskCreationOptions.RunContinuationsAsynchronously;

    private readonly Action _action;

    private readonly TimeSpan _interval;

    private readonly bool _repeat;

    private readonly TimeProvider _timeProvider;

    private readonly CancellationTokenSource _cancellationSource;

    private readonly object _cancellationLock = new();

    private Task? _cancellationCompletion;

    private int _resourcesDisposed;

    internal Task Completion { get; }

    internal bool IsCancellationRequested
    {
        get
        {
            lock (_cancellationLock)
            {
                return _cancellationSource.IsCancellationRequested;
            }
        }
    }

    internal Task CancellationCompletion
    {
        get
        {
            lock (_cancellationLock)
            {
                return _cancellationCompletion ?? Task.CompletedTask;
            }
        }
    }

    internal ScheduledTask(
        Action action,
        int timeoutMs,
        bool repeat,
        TimeProvider timeProvider)
        : this(
            action ?? throw new ArgumentNullException(nameof(action)),
            ValidateInterval(timeoutMs),
            repeat,
            timeProvider ?? throw new ArgumentNullException(nameof(timeProvider)),
            new CancellationTokenSource())
    { }

    private ScheduledTask(
        Action action,
        TimeSpan interval,
        bool repeat,
        TimeProvider timeProvider,
        CancellationTokenSource cancellationSource)
        : base(cancellationSource.Token, ScheduleCreationOptions)
    {
        _action = action;
        _interval = interval;
        _repeat = repeat;
        _timeProvider = timeProvider;
        _cancellationSource = cancellationSource;
        Completion = this.Unwrap();
    }

    internal Task RequestCancellation()
    {
        lock (_cancellationLock)
        {
            if (_resourcesDisposed != 0)
            {
                return _cancellationCompletion ?? Task.CompletedTask;
            }

            _cancellationCompletion ??= _cancellationSource.CancelAsync();
            return _cancellationCompletion;
        }
    }

    internal void AbandonAfterStartFailure()
    {
        _ = DisposeAfterAbandonAsync(RequestCancellation());
    }

    protected override Task FunctionTask()
    {
        return _repeat ? RunPeriodicAsync() : RunOnceAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCancellationSource();
        }

        base.Dispose(disposing);
    }

    private async Task RunOnceAsync()
    {
        CancellationToken cancellation = _cancellationSource.Token;

        await Task.Delay(_interval, _timeProvider, cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        _action();
    }

    private async Task RunPeriodicAsync()
    {
        CancellationToken cancellation = _cancellationSource.Token;
        using PeriodicTimer timer = new(_interval, _timeProvider);

        while (await timer.WaitForNextTickAsync(cancellation).ConfigureAwait(false))
        {
            cancellation.ThrowIfCancellationRequested();
            _action();
        }
    }

    private async Task DisposeAfterAbandonAsync(Task cancellationCompletion)
    {
        try
        {
            await cancellationCompletion.ConfigureAwait(false);
        }
        catch
        {
            // The start failure remains the primary exception.
        }
        finally
        {
            DisposeCancellationSource();
        }
    }

    private void DisposeCancellationSource()
    {
        lock (_cancellationLock)
        {
            if (_resourcesDisposed == 0)
            {
                _resourcesDisposed = 1;
                _cancellationSource.Dispose();
            }
        }
    }

    private static TimeSpan ValidateInterval(int timeoutMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        return TimeSpan.FromMilliseconds(timeoutMs);
    }
}
