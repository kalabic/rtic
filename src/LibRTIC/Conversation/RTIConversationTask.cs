using AudioFormatLib;
using AudioFormatLib.Buffers;
using AudioFormatLib.IO;
using LibRTIC.Config;
using LibRTIC.Conversation.UpdatesReceiver;
using LibRTIC.MiniTaskLib;
using LibRTIC.MiniTaskLib.Events;
using LibRTIC.MiniTaskLib.Model;
using OpenAI.Realtime;
using System.Net.WebSockets;
using static LibRTIC.Conversation.FailedToConnect;

namespace LibRTIC.Conversation;

#pragma warning disable OPENAI002


public class RTIConversationTask : RTIConversation
{
    public static RTIConversation Create(Info info, CancellationToken cancellation)
    {
        return new RTIConversationTask(info, cancellation);
    }

    // Just in case, let's keep connection opening timeout under 20  sec.
    private const int START_TASK_TIMEOUT = 20000;

    private const int STOP_TASK_TIMEOUT = 10000;

    private const int AUDIO_INPUT_PACKET_MINIMUM = 2048;

    private const int AUDIO_INPUT_PACKET = (AUDIO_INPUT_PACKET_MINIMUM * 8);

    private const int INPUT_AUDIO_ACTION_PERIOD = 200;

    private const int WAIT_MINIMUM_DATA_MS = 100;


    /// <summary>
    /// All events from this _collection are (should be) forwarded to <see cref="ReceiverQueue"/>,
    /// but here made available for handling directly.
    /// </summary>
    public override EventCollection ReceiverEvents { get { return _receiverTaskEvents; } }

    public override EventQueue ConversationEvents { get { return _receiver.ReceiverEvents; } }

    public ConversationUpdatesReceiver Receiver {  get { return _receiver; } }



    protected readonly Info _info;

    private CancellationTokenSource _startCanceller;

    private TaskWithEvents? _networkConnectionTask = null;

    private TaskWithEvents? _sendAudioTask = null;

    private CancellationTokenSource? _audioCancellation = null;

    private IAudioBufferOutput? _audioOutputStream = null;

    private AudioStreamBuffer? _internalAudioBuffer = null;

    private RealtimeClient? _client = null;

    private ConversationOptions? _options = null;

    private ConversationUpdatesReceiver _receiver;

    private CancellationToken _cancellation;

    private object _lockRTE = new object();

    private EventCollection _receiverTaskEvents;

    protected RTIConversationTask(Info info, CancellationToken cancellation)
    {
        this._info = info;
        this._startCanceller = new CancellationTokenSource();
        this._receiverTaskEvents = new EventCollection(info, "ConversationUpdatesReceiverTask Events");
        this._cancellation = cancellation;
        this._receiver = new ConversationUpdatesReceiver(info);


        var receiverQueue = _receiver.ReceiverEvents;

        // Forward events invoked from whichever task to be handled using 'receiverQueue' task.
        receiverQueue.ForwardFrom<ClientStartedConnecting>(_receiverTaskEvents);
        receiverQueue.ForwardFrom<InputAudioTaskFinished>(_receiverTaskEvents);
        receiverQueue.ForwardFrom<FailedToConnect>(_receiverTaskEvents);
        receiverQueue.ForwardFrom<TaskExceptionOccured>(_receiverTaskEvents);

        // Connect event handlers.
        receiverQueue.Connect<InputAudioTaskFinished>(HandleEvent);
        receiverQueue.Connect<FailedToConnect>(HandleEvent);
        receiverQueue.Connect<MessageQueueStarted>(HandleEvent);
    }

    /// <summary>
    /// WIP, not used at all for now.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="audioInputStream"></param>
    public override void ConfigureWith(RealtimeClient client, IAudioBufferOutput audioOutputStream)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// WIP, 'ConversationOptions' not used properly. Session options always loaded from
    /// <see cref="ConversationSessionConfig.GetDefaultConversationSessionOptions"/>.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="audioInputStream"></param>
    public override void ConfigureWith(ConversationOptions options, IAudioBufferOutput audioOutputStream)
    {
        this._options = options;
        this._audioOutputStream = audioOutputStream;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _startCanceller?.Dispose();
            //_audioOutputStream?.Dispose();
            _audioOutputStream = null;
            _internalAudioBuffer?.Dispose();
            _internalAudioBuffer = null;
            _receiver.Dispose();
            _audioCancellation?.Dispose();
            _audioCancellation = null;
            _client = null;

            lock (_lockRTE)
            {
                _receiverTaskEvents.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Runs conversation session synchronously. By the time it returns, complete shutdown should have been initiated and done.
    /// </summary>
    public override void Run()
    {
        _receiver.Run();
        AssertAllTasksComplete();
    }

    public override Task RunAsync()
    {
        var receiverQueueTask = _receiver.RunAsync();
        receiverQueueTask.TaskEvents.Connect<TaskCompleted>( AssertAllTasksComplete );
        return receiverQueueTask;
    }

    public override void Await()
    {
        var awaiter = _receiver.GetAwaiter();
        awaiter?.Wait();
    }
     
    public override async Task AwaitAsync(CancellationToken finalCancellation)
    {
        var awaiter = _receiver.GetAwaiter();
        if (awaiter is not null)
        {
            await awaiter;
        }
    }

    public override TaskWithEvents? GetAwaiter()
    {
        return _receiver.GetAwaiter();
    }

    /// <summary>
    /// Initiates end of conversation session and returns immediatelly. Should be used only when receiver is running
    /// in asynchronous mode. Shutdown always begings with stopping audio input tasks. In fact, if they are completed 
    /// or broken for any reason that alone should trigger end of session and complete shutdown by itself.
    /// </summary>
    public override void Cancel()
    {
        _startCanceller?.CancelAsync();
        _internalAudioBuffer?.CloseBuffer();
        _receiver.CancelMicrophone();
        _receiver.FinishReceiver();
    }

    /// <summary>
    /// List of all tasks started by this class, with the exception of the 'awaiter' task, 'awaiter' task exists when
    /// <see cref="ConversationUpdatesReceiver"/> is running message queue in asynchronous mode and should not be 
    /// included in this list.
    /// </summary>
    /// <returns></returns>
    public override List<TaskWithEvents> GetTaskList()
    {
        List<TaskWithEvents> list = new();
        if (_networkConnectionTask is not null)
        {
            list.Add(_networkConnectionTask);
        }
        if (_sendAudioTask is not null)
        {
            list.Add(_sendAudioTask);
        }
        return list;
    }

    /// <summary>
    /// When running in synchronous mode using method <see cref="Run"/>, this is invoked after return from
    /// main message queue loop to assert all other tasks started by this class are finished.
    /// </summary>
    private void AssertAllTasksComplete()
    {
        InternalCancelStopDisposeAll();
    }

    /// <summary>
    /// When running in asynchronous mode using method <see cref="RunAsync"/>, this is invoked after return
    /// from main message queue loop to assert all other tasks started by this class are finished.
    /// </summary>
    private void AssertAllTasksComplete(object? sender, TaskCompleted update)
    {
        InternalCancelStopDisposeAll();
    }

    /// <summary>
    /// Throws exception if updates receiver task already exists.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private void StartNetworkConnectionTask()
    {
        if (_networkConnectionTask is null)
        {
            _receiver.DelayedAction(NetworkConnectionWatchdog, START_TASK_TIMEOUT);

            _networkConnectionTask = new ActionTask(_info, NetworkConnectionEntry );
            _networkConnectionTask.TaskEvents.Connect<TaskExceptionOccured>( HandleEvent );
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
        if (!_receiver.IsWebSocketOpen)
        {
            _startCanceller.Cancel();
        }
    }

    /// <summary>
    /// Entry for a task that establishes network connection with the server and receives conversation updates.
    /// <para>Conversation updates are enqued into main message queue and application shoould read them using it.</para>
    /// </summary>
    /// <param name="networkTaskCancellation"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private void NetworkConnectionEntry(CancellationToken networkTaskCancellation)
    {
        if (_client is not null)
        {
            throw new InvalidOperationException("Updates receiver object is not reusable.");
        }

        if ((_options is null) || (_options._client is null))
        {
            FailedToConnect(ErrorStatus.EndpointOptionsMissing, "Realtime endpoint API options are missing.");
            return;
        }

        ClientStartedConnecting(_options._client.Type);

        // TODO: Switch to asynchronous client initialization.
        _client = ConfiguredClient.FromOptions(_info, _options._client);
        if (_client is null)
        {
            FailedToConnect(ErrorStatus.FailedToConfigure, "Failed to configure OpenAI's realtime client from provided endpoint API options.");
            return;
        }

        RealtimeSession? session = null;
        try
        {
            session = _client.StartConversationSession(_options._client.AOAIDeployment, _startCanceller.Token);
            var options = ConversationSessionConfig.GetDefaultConversationSessionOptions();
            session.ConfigureSession(options, _startCanceller.Token);
        }
        catch (OperationCanceledException ex)
        {
            if (!_startCanceller.IsCancellationRequested)
            {
                // 'startWatchdog' did not trigger cancellation, so reason for exception cannot be clearly known.
                FailedToConnect(ErrorStatus.Unknown, "Network connection canceled for unknown reason.\n" + TaskTool.BuildMultiLineExceptionErrorString(ex));
            }
            else if (networkTaskCancellation.IsCancellationRequested || _cancellation.IsCancellationRequested)
            {
                // Cancellation because some of wait handles observed by 'startWatchdog' were triggered.
                FailedToConnect(ErrorStatus.ConnectionCanceled, "Network connection canceled.");
            }
            else
            {
                // Cancellation because 'START_TASK_TIMEOUT' used by 'startWatchdog' was triggered.
                FailedToConnect(ErrorStatus.ServerDidNotRespond, "Network connection canceled because server did not respond in time.");
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
            FailedToConnect(ErrorStatus.ServerDidNotRespond, TaskTool.BuildMultiLineExceptionErrorString(ex));
            return;
        }

        _receiver.SetSession(session);

        // 'Session Started Update' event is used to start sending microphone input to the server.
        _receiver.ReceiverEvents.Connect<ConversationSessionConfigured>(StartAudioInputTask);

        _receiver.ReceiveUpdates(networkTaskCancellation);
    }

    /// <summary>
    /// Invoked as an event handler for <see cref="ConversationSessionConfigured"/> that is connected to this method if
    /// function <see cref="NetworkConnectionEntry"/> has managed to connect with the server.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private void StartAudioInputTask(object? sender, ConversationSessionConfigured update)
    {
        if (_receiver is null)
        {
            throw new InvalidOperationException("Updates receiver object does not exist.");
        }
        if (_networkConnectionTask is null)
        {
            throw new InvalidOperationException("Network Connection task does not exist.");
        }
        else if (_networkConnectionTask.Status != TaskStatus.Running)
        {
            throw new InvalidOperationException("Network Connection task is not running.");
        }

        if (_sendAudioTask is not null)
        {
            throw new InvalidOperationException("Send audio task already created.");
        }

        _audioCancellation = CancellationTokenSource.CreateLinkedTokenSource(_receiver.Cancellation.MicrophoneToken, _cancellation);

        //
        // An intermediate buffer between 'send audio' task and input audio source (microphone). TODO: Will be useful later.
        //
        var format = new ABufferParams(ConversationSessionConfig.AudioFormat);
        format.BufferSize = (int)format.Format.BufferSizeFromSeconds(ConversationSessionConfig.AUDIO_INPUT_BUFFER_SECONDS);
        format.WaitForCompleteRead = true;
        _internalAudioBuffer = new AudioStreamBuffer(format, _audioCancellation.Token);
        // _internalAudioBuffer.SetWaitMinimumData(WAIT_MINIMUM_DATA_MS);

        //
        // A task that reads input audio from intermediate buffer and sends it to the server.
        //
        _sendAudioTask = new ActionTask(_info, SendAudioInputTask );
        _sendAudioTask.TaskEvents.ConnectAsync<TaskCompleted>(AudioInputFinished);
        _sendAudioTask.Start();

        _receiver.RepeatAction(InputAudioAction, INPUT_AUDIO_ACTION_PERIOD);
    }

    /// <summary>
    /// This method is running as a separate task that enters <see cref="RealtimeConversationSession.SendInputAudio(Stream, CancellationToken)"/>
    /// and is running in a loop inside.
    /// </summary>
    /// <param name="cancellation"></param>
    private void SendAudioInputTask(CancellationToken cancellation)
    {
        if ((_internalAudioBuffer is not null) && (_audioCancellation is not null))
        {
            _receiver.SendInputAudio(_internalAudioBuffer.Output.Stream, _audioCancellation.Token);
            _receiver.ClearInputAudio();
        }
    }

    /// <summary>
    /// This method is invoked every <see cref="INPUT_AUDIO_ACTION_PERIOD"/> miliseconds.
    /// </summary>
    private void InputAudioAction()
    {
        if (_internalAudioBuffer is not null && _audioOutputStream is not null &&
            _audioCancellation is not null && !_audioCancellation.IsCancellationRequested)
        {
            int bytesRead = -1;
            byte[] buffer = new byte[AUDIO_INPUT_PACKET];

            // Try to read complete chunks of size 'AUDIO_INPUT_PACKET_MINIMUM'.
            while (!_audioCancellation.IsCancellationRequested && 
                   (_internalAudioBuffer.AvailableSpace >= AUDIO_INPUT_PACKET) &&
                   bytesRead != 0)
            {
                bytesRead = _audioOutputStream.Read(buffer, 0, AUDIO_INPUT_PACKET);
                if (bytesRead > 0)
                {
                    _internalAudioBuffer.Input.Stream.Write(buffer, 0, bytesRead);
                }
            }
        }
    }

    protected void HandleSessionExceptions(Action sessionFunction)
    {
        try
        {
            sessionFunction();
        }
        catch (WebSocketException ex)
        {
            _info.Info("WebSocket connection closed: " + ex.Message);
        }
        catch (OperationCanceledException ex)
        {
            _info.Info("Session canceled: " + ex.Message);
        }
        catch (Exception ex)
        {
            _info.ExceptionOccured(ex);
            TaskExceptionOccurred(ex);
        }
    }

    /// <summary>
    /// Forwarded to <see cref="HandleEvent(object?,InputAudioTaskFinished)"/>.
    /// </summary>
    private void AudioInputFinished(object? s, TaskCompleted ev)
    {
        InvokeReceiverTaskEvent(new InputAudioTaskFinished());
    }

    /// <summary>
    /// Forwarded to <see cref="HandleEvent(object?,FailedToConnect)"/>.
    /// </summary>
    private void FailedToConnect(FailedToConnect.ErrorStatus errorStatus, string message)
    {
        InvokeReceiverTaskEvent(new FailedToConnect(errorStatus, message));
    }

    private void ClientStartedConnecting(EndpointType endpointType)
    {
        InvokeReceiverTaskEvent(new ClientStartedConnecting(endpointType));
    }

    private void TaskExceptionOccurred(Exception ex)
    {
        InvokeReceiverTaskEvent(new TaskExceptionOccured(ex));
    }

    private void InvokeReceiverTaskEvent<TMessage>(TMessage message)
    {
        lock (_lockRTE)
        {
            if (!_receiverTaskEvents.IsComplete)
            {
                _receiverTaskEvents.Invoke(message);
            }
        }
    }

    /// <summary>
    /// Entry for <see cref="MessageQueueStarted"/> event notification.
    /// </summary>
    private void HandleEvent(object? sender, MessageQueueStarted update)
    {
        StartNetworkConnectionTask();
    }

    /// <summary>
    /// Forwarded from <see cref="AudioInputFinished"/>.
    /// </summary>
    private void HandleEvent(object? sender, InputAudioTaskFinished update)
    {
        _receiver.FinishReceiver(); // This should start graceful shutdown.
        InternalCancelStopDisposeAll();
        _receiver.CloseMessageQueue(); // The end.
    }

    /// <summary>
    /// Forwarded from <see cref="FailedToConnect"/>.
    /// </summary>
    private void HandleEvent(object? sender, FailedToConnect update)
    {
        InternalCancelStopDisposeAll();
        _receiver.CloseMessageQueue(); // The end.
    }
    
    public void HandleEvent(object? sender, TaskExceptionOccured update)
    {
        Cancel();
        _info.ExceptionOccured(update.Exception);
    }

    private void InternalCancelStopDisposeAll()
    {
        var taskList = GetTaskList();
        TaskTool.CancelAndWaitAll(taskList, STOP_TASK_TIMEOUT);
    }
}
