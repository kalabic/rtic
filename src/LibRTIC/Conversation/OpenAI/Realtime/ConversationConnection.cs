using AudioFormatLib.IO;
using DotBase.Event;
using DotBase.Log;
using LibRTIC.Config;
using LibRTIC.Conversation.UpdatesReceiver;
using LibRTIC.MiniTaskLib;
using LibRTIC.MiniTaskLib.Events;
using LibRTIC.MiniTaskLib.Queues;
using LibRTIC.Realtime;
using OpenAI.Realtime;
using System.Net.WebSockets;

namespace LibRTIC.Conversation.OpenAI.Realtime;

#pragma warning disable OPENAI002


internal class ConversationConnection : ConversationUpdatesReceiver
{
    // Just in case, let's keep connection opening timeout under 15 sec.
    private const int START_TASK_TIMEOUT = 15000;

    // Total timeout that includes connection opening, a timeout until
    // connected client can be proclaimed to be operational.
    private const int START_OPERATION_TIMEOUT = 20000;

    private const int STOP_TASK_TIMEOUT = 10000;

    private EventProducerCollection _conversationEvents;

    private RealtimeClient? _client = null;

    private RTICConfig? _options = null;

    private TaskWithEvents? _networkConnectionTask = null;

    private CancellationTokenSource _startCanceller = new CancellationTokenSource();

    private readonly CancellationTokenSource _startupWatchdogCancellation = new();

    private int _startupFailureReported = 0;

    public ConversationConnection(InfoLog info, EventProducerCollection conversationEvents, ConversationCancellation cancellation) 
        : base(info, cancellation)
    {
        _conversationEvents = conversationEvents;

        var receiverEvents = ReceiverEvents;

        // Forward events invoked from any task to handlers dispatched through the action queue.
        receiverEvents.ForwardFrom<ClientStartedConnecting>(conversationEvents);
        receiverEvents.ForwardFrom<InputAudioTaskFinished>(conversationEvents);
        receiverEvents.ForwardFrom<FailedToConnectMsg>(conversationEvents);
        receiverEvents.ForwardFrom<FailedToOperateMsg>(conversationEvents);
        receiverEvents.ForwardFrom<TaskExceptionOccured>(conversationEvents);

        // Connect event handlers.
        receiverEvents.Connect<InputAudioTaskFinished>(HandleEvent);
        receiverEvents.Connect<FailedToConnectMsg>(HandleEvent);
        receiverEvents.Connect<FailedToOperateMsg>(HandleEvent);
        receiverEvents.Connect<ActionQueueStarted>(HandleEvent);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _startupWatchdogCancellation.Cancel();
            _startupWatchdogCancellation.Dispose();
            _startCanceller?.Dispose();
            _client = null;
        }

        base.Dispose(disposing);
    }

    public override void ConfigureWith(RTICConfig options)
    {
        _options = options;
    }

    /// <summary>
    /// List of all tasks started by this class, with the exception of the 'awaiter' task, 'awaiter' task exists when
    /// <see cref="ConversationUpdatesReceiver"/> is running its action queue asynchronously and should not be
    /// included in this list.
    /// </summary>
    /// <returns></returns>
    public List<TaskWithEvents> GetTaskList()
    {
        List<TaskWithEvents> list = new();
        if (_networkConnectionTask is not null)
        {
            list.Add(_networkConnectionTask);
        }
        return list;
    }

    private void HandleEvent(object? sender, TaskCompleted update)
    {
        // The receiver task is the authoritative lifetime of the network session.
        // Do not depend on the microphone task to complete the action queue: cancellation
        // must still complete if audio code is stuck or broken.
        CompleteAdding();
    }

    /// <summary>
    /// Entry for <see cref="ActionQueueStarted"/> event notification.
    /// </summary>
    private void HandleEvent(object? sender, ActionQueueStarted update)
    {
        StartNetworkConnectionTask();
    }

    /// <summary>
    /// Forwarded from <see cref="AudioInputFinished"/>.
    /// </summary>
    private void HandleEvent(object? sender, InputAudioTaskFinished update)
    {
        FinishReceiver(); // This should start graceful shutdown.
        InternalCancelStopDisposeAll();
        CompleteAdding(); // The end.
    }

    /// <summary>
    /// Forwarded from <see cref="FailedToConnect"/>.
    /// </summary>
    private void HandleEvent(object? sender, FailedToConnectMsg update)
    {
        InternalCancelStopDisposeAll();
        CompleteAdding(); // The end.
    }

    /// <summary>
    /// Note for reviewer: Maybe it should be responsibility of application layer to decide to shutdown connection in this case?
    /// Forwarded from <see cref="FailedToOperate"/>.
    /// </summary>
    private void HandleEvent(object? sender, FailedToOperateMsg update)
    {
        FinishReceiver();
        _info.Error("Conversation connection failed to operate.");
    }

    public void HandleEvent(object? sender, TaskExceptionOccured update)
    {
        FinishReceiver();
        _info.Error("Conversation task failed.", update.Exception);
    }

    public override void FinishReceiver()
    {
        _startCanceller?.CancelAsync();
        base.FinishReceiver();
    }

    /// <summary>
    /// Throws exception if updates receiver task already exists.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private void StartNetworkConnectionTask()
    {
        if (_networkConnectionTask is null)
        {
            DelayedAction(NetworkConnectionWatchdog, START_TASK_TIMEOUT);
            _ = MonitorConversationOperationAsync(_startupWatchdogCancellation.Token);

            _networkConnectionTask = new ActionTask(_info, NetworkConnectionEntry);
            _networkConnectionTask.TaskEvents.Connect<TaskExceptionOccured>(HandleEvent);
            _networkConnectionTask.TaskEvents.Connect<TaskCompleted>(HandleEvent);
#if DEBUG
            _networkConnectionTask.SetLabel("Network Connection");
#endif
            _networkConnectionTask.Start();
        }
        else
        {
            throw new InvalidOperationException("Network Connection task already created.");
        }
    }

    /// <summary>
    /// Invoked <see cref="START_TASK_TIMEOUT"/> miliseconds after network task was started to check
    /// if connection with server was established or not.
    /// </summary>
    private void NetworkConnectionWatchdog()
    {
        if (!IsWebSocketOpen)
        {
            _startCanceller.Cancel();
        }
    }

    /// <summary>
    /// Runs independently from the receiver action queue and network task. Opening a
    /// WebSocket is not sufficient: the session and its required microphone pipeline
    /// must both become operational before the startup deadline.
    /// </summary>
    private async Task MonitorConversationOperationAsync(CancellationToken cancellation)
    {
        try
        {
            await Task.Delay(START_OPERATION_TIMEOUT, cancellation).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }

        if (_startCanceller.IsCancellationRequested || 
            cancellation.IsCancellationRequested ||
            Cancellation.IsCancellationRequested ||
            (IsWebSocketOpen &&
             SessionState.SessionStarted &&
             SessionState.InputAudioRunning))
        {
            return;
        }

        _ = _startCanceller.CancelAsync();
        FailedToOperate(
            FailedToOperateMsg.ErrorStatus.Unknown,
            $"Conversation did not become operational within {START_OPERATION_TIMEOUT} ms. " +
            $"WebSocketOpen={IsWebSocketOpen}, " +
            $"SessionStarted={SessionState.SessionStarted}, " +
            $"InputAudioRunning={SessionState.InputAudioRunning}.");
    }

    /// <summary>
    /// Entry for a task that establishes network connection with the server and receives conversation updates.
    /// <para>Conversation updates are enqueued into the main action queue for application handlers.</para>
    /// </summary>
    /// <param name="networkTaskCancellation"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private void NetworkConnectionEntry(CancellationToken networkTaskCancellation)
        => NetworkConnectionEntryAsync(networkTaskCancellation).GetAwaiter().GetResult();

    private async Task NetworkConnectionEntryAsync(CancellationToken networkTaskCancellation)
    {
        if (_client is not null)
        {
            throw new InvalidOperationException("Updates receiver object is not reusable.");
        }

        if (_options is null)
        {
            FailedToConnect(FailedToConnectMsg.ErrorStatus.EndpointOptionsMissing, "Realtime endpoint API options are missing.");
            return;
        }

        ClientStartedConnecting(_options.Provider.Type);

        RealtimeSessionClient? session = null;
        try
        {
            RealtimeClientFactory.StartedRealtimeSession startedSession =
                await RealtimeClientFactory.StartConversationSessionAsync(
                    _options.Provider,
                    _startCanceller.Token).ConfigureAwait(false);

            if (startedSession is null)
            {
                FailedToConnect(
                    FailedToConnectMsg.ErrorStatus.FailedToConfigure,
                    "Failed to configure OpenAI's realtime client from provided endpoint API options.");
                return;
            }

            _client = startedSession.Client;
            session = startedSession.Session;
            var options = RealtimeSessionOptionsFactory.Create(_options.Session);
            await session.ConfigureConversationSessionAsync(
                options,
                _startCanceller.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            if (!_startCanceller.IsCancellationRequested)
            {
                // 'startWatchdog' did not trigger cancellation, so reason for exception cannot be clearly known.
                FailedToConnect(FailedToConnectMsg.ErrorStatus.Unknown, "Network connection canceled for unknown reason.\n" + TaskTool.BuildMultiLineExceptionErrorString(ex));
            }
            else if (networkTaskCancellation.IsCancellationRequested || Cancellation.IsCancellationRequested)
            {
                // Cancellation because some of wait handles observed by 'startWatchdog' were triggered.
                FailedToConnect(FailedToConnectMsg.ErrorStatus.ConnectionCanceled, "Network connection canceled.");
            }
            else
            {
                // Cancellation because 'START_TASK_TIMEOUT' used by 'startWatchdog' was triggered.
                FailedToConnect(FailedToConnectMsg.ErrorStatus.ServerDidNotRespond, "Network connection canceled because server did not respond in time.");
            }

            session?.Dispose();
            return;
        }
        catch (WebSocketException ex)
        {
            //
            // When OpenAI.RealtimeConversation client gives up, it will throw here.
            //
            session?.Dispose();
            FailedToConnect(FailedToConnectMsg.ErrorStatus.ServerDidNotRespond, TaskTool.BuildMultiLineExceptionErrorString(ex));
            return;
        }

        SetSession(session);
        ReceiveUpdates(networkTaskCancellation);
    }

    /// <summary>
    /// Running in a loop.
    /// </summary>
    /// <param name="cancellation"></param>
    public override void SendInputAudio(IAudioStreamOutput stream, CancellationToken cancellation)
    {
        SendInputAudioStream(stream, cancellation);
        ClearInputAudio();
    }

    private void InvokeConversationEvent<TMessage>(TMessage message)
    {
        try
        {
            _conversationEvents.Invoke(message);
        }
        catch (Exception ex)
        {
            _info.Warning("Exception while invoking receiver task event handlers.", ex);
        }
    }

    private void ClientStartedConnecting(RTICProviderType providerType)
    {
        InvokeConversationEvent(new ClientStartedConnecting(providerType));
    }

    /// <summary>
    /// Forwarded to <see cref="HandleEvent(object?,FailedToConnectMsg)"/>.
    /// </summary>
    private void FailedToConnect(FailedToConnectMsg.ErrorStatus errorStatus, string message)
    {
        if (Interlocked.Exchange(ref _startupFailureReported, 1) != 0)
        {
            return;
        }

        _startupWatchdogCancellation.Cancel();
        InvokeConversationEvent(new FailedToConnectMsg(errorStatus, message));
    }

    /// <summary>
    /// Forwarded to <see cref="HandleEvent(object?,FailedToOperateMsg)"/>.
    /// Used to report various reasons when conected client stops to operate.
    /// </summary>
    private void FailedToOperate(FailedToOperateMsg.ErrorStatus errorStatus, string message)
    {
        InvokeConversationEvent(new FailedToOperateMsg(errorStatus, message));
    }

    private void InternalCancelStopDisposeAll()
    {
        var taskList = GetTaskList();
#if !DEBUG
        TaskTool.CancelAndWaitAll(taskList, STOP_TASK_TIMEOUT);
#else
        long finishMs = TaskTool.CancelAndWaitAll(taskList, STOP_TASK_TIMEOUT);
        if (finishMs > 0)
        {
            _info.Info($"It took {finishMs} ms to close session.");
        }
        else if (finishMs < 0)
        {
            _info.Error("Failed to finish session. Some conversation receiver tasks still running.");
        }
#endif
    }
}
