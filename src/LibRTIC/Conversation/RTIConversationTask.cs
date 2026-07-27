using AudioFormatLib;
using AudioFormatLib.Buffers;
using AudioFormatLib.IO;
using DotBase.Event;
using DotBase.Log;
using LibRTIC.Config;
using LibRTIC.Conversation.OpenAI.Realtime;
using LibRTIC.Conversation.UpdatesReceiver;
using LibRTIC.MiniTaskLib;
using LibRTIC.MiniTaskLib.Events;
using LibRTIC.Realtime;
using System.Net.WebSockets;

namespace LibRTIC.Conversation;


public sealed class RTIConversationTask : RTIConversation
{
    public static RTIConversation Create(InfoLog info, CancellationToken cancellation)
    {
        return new RTIConversationTask(info, cancellation);
    }

    private const int STOP_TASK_TIMEOUT = 10000;

    private const int AUDIO_INPUT_FRAME_CAPACITY = 8_192;

    private const int INPUT_AUDIO_ACTION_PERIOD = 200;

    /// <summary>
    /// All events from this collection are forwarded to <see cref="UpdatesReceiverEvents"/>,
    /// but here made available for handling directly.
    /// </summary>
    public override EventProducerCollection ConversationEvents { get { return _conversationEvents; } }

    public override EventQueue UpdatesReceiverEvents { get { return _receiver.ReceiverEvents; } }

    private readonly InfoLog _info;

    private TaskWithEvents? _sendAudioTask = null;

    private CancellationToken _cancellation;

    private ConversationCancellation _conversationCancellation;

    private CancellationTokenSource? _audioCancellation = null;

    private IPcm16FrameOutput? _audioOutputFrames = null;

    private AudioStreamBuffer? _internalAudioBuffer = null;

    private IPcm16FrameInput? _internalAudioInput = null;

    private RTICUpdatesReceiver _receiver;

    private EventProducerCollection _conversationEvents;

    private int _cancelRequested = 0;

    private RTIConversationTask(InfoLog info, CancellationToken cancellation)
    {
        _info = info;
        _conversationEvents = new EventProducerCollection("RTIConversationTask Events");
        _cancellation = cancellation;
        _conversationCancellation = new ConversationCancellation(_cancellation);
        _receiver = new ConversationConnection(info, _conversationEvents, _conversationCancellation);
        UpdatesReceiverEvents.Connect<RTICSessionConfigured>(StartAudioInputTask);
    }

    public override void ConfigureWith(RTICConfig options, IPcm16FrameOutput audioOutputFrames)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(audioOutputFrames);
        if (audioOutputFrames.Format.SampleRate != RealtimeAudioContract.SamplesPerSecond ||
            audioOutputFrames.Format.ChannelCount != RealtimeAudioContract.ChannelCount)
        {
            throw new ArgumentException(
                "Realtime microphone audio must be mono PCM16 at " +
                $"{RealtimeAudioContract.SamplesPerSecond} frames per second.",
                nameof(audioOutputFrames));
        }

        _audioOutputFrames = audioOutputFrames;
        _receiver.ConfigureWith(options);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _audioOutputFrames = null;
            _internalAudioInput = null;
            _internalAudioBuffer?.Dispose();
            _internalAudioBuffer = null;
            _receiver.Dispose();
            _audioCancellation?.Dispose();
            _audioCancellation = null;
            _conversationEvents.Dispose();
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
        var receiverActionQueueTask = _receiver.RunAsync();
        receiverActionQueueTask.TaskEvents.Connect<TaskCompleted>( AssertAllTasksComplete );
        return receiverActionQueueTask;
    }

    private protected override Task RequestResponseCoreAsync(
        RTICResponseRequest request,
        CancellationToken cancellationToken)
        => _receiver.RequestResponseAsync(request, cancellationToken);

    private protected override Task InterruptOutputCoreAsync(
        RTICOutputInterruption request,
        CancellationToken cancellationToken)
        => _receiver.InterruptOutputAsync(request, cancellationToken);

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
        if (Interlocked.Exchange(ref _cancelRequested, 1) != 0)
        {
            return;
        }

        // CancellationTokenSource.Cancel() executes callbacks synchronously. A callback,
        // buffer, provider, or task can regress and block indefinitely, so terminal
        // cancellation must be signalled without borrowing the caller's thread.
        _conversationCancellation.CancelConversation();

        _ = Task.Run(RequestGracefulShutdown);
        _ = EnforceCancellationTimeoutAsync();
    }

    private void RequestGracefulShutdown()
    {
        try
        {
            _internalAudioBuffer?.CloseBuffer();
        }
        catch (Exception ex)
        {
            _info.Warning("Failed to close the conversation audio buffer.", ex);
        }

        try
        {
            _receiver.FinishReceiver();
        }
        catch (Exception ex)
        {
            _info.Warning("Failed to initiate graceful conversation shutdown.", ex);
        }
    }

    private async Task EnforceCancellationTimeoutAsync()
    {
        await Task.Delay(STOP_TASK_TIMEOUT).ConfigureAwait(false);

        TaskWithEvents? awaiter = _receiver.GetAwaiter();
        if (awaiter is not null && !awaiter.IsCompleted)
        {
            _info.Warning(
                $"Conversation shutdown did not complete within {STOP_TASK_TIMEOUT} ms; " +
                "completing its action queue.");
            _conversationCancellation.CancelWebSocket();
            _receiver.CompleteAdding();
        }
    }

    /// <summary>
    /// List of all tasks started by this class, with the exception of the 'awaiter' task, 'awaiter' task exists when
    /// <see cref="ConversationUpdatesReceiver"/> is running its action queue asynchronously and should not be
    /// included in this list.
    /// </summary>
    /// <returns></returns>
    public override List<TaskWithEvents> GetTaskList()
    {
        List<TaskWithEvents> list = new();
        if (_sendAudioTask is not null)
        {
            list.Add(_sendAudioTask);
        }
        return list;
    }

    /// <summary>
    /// When running in synchronous mode using method <see cref="Run"/>, this is invoked after return from
    /// main action queue loop to assert all other tasks started by this class are finished.
    /// </summary>
    private void AssertAllTasksComplete()
    {
        InternalCancelStopDisposeAll();
    }

    /// <summary>
    /// When running in asynchronous mode using method <see cref="RunAsync"/>, this is invoked after return
    /// from main action queue loop to assert all other tasks started by this class are finished.
    /// </summary>
    private void AssertAllTasksComplete(object? sender, TaskCompleted update)
    {
        InternalCancelStopDisposeAll();
    }

    /// <summary>
    /// Invoked as an event handler for <see cref="RTICSessionConfigured"/> that is connected to this method if
    /// function <see cref="NetworkConnectionEntry"/> has managed to connect with the server.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private void StartAudioInputTask(object? sender, RTICSessionConfigured update)
    {
        if (_sendAudioTask is not null)
        {
            return;
        }

        if (_receiver is null)
        {
            throw new InvalidOperationException("Updates receiver object does not exist.");
        }

        _audioCancellation = CancellationTokenSource.CreateLinkedTokenSource(_receiver.Cancellation.MicrophoneToken, _cancellation);

        //
        // An intermediate buffer between 'send audio' task and input audio source (microphone). TODO: Will be useful later.
        //
        var format = new ABufferParams(RealtimeAudioContract.AudioFormat);
        format.BufferSize = (int)format.Format.BufferSizeFromSeconds(RealtimeAudioContract.InputBufferSeconds);
        format.WaitForCompleteRead = true;
        _internalAudioBuffer = new AudioStreamBuffer(format, _audioCancellation.Token);
        _internalAudioInput = _internalAudioBuffer.Input.Pcm16Frames
            ?? throw new InvalidOperationException(
                "The internal Realtime audio buffer is not PCM16-compatible.");
        //
        // A task that reads input audio from intermediate buffer and sends it to the server.
        //
        _sendAudioTask = new ActionTask(_info, SendAudioInputTask );
#if DEBUG
        _sendAudioTask.SetLabel("Send Audio");
#endif
        _sendAudioTask.TaskEvents.ConnectAsync<TaskCompleted>(AudioInputFinished);
        _sendAudioTask.Start();
        _receiver.SessionState.InputAudioRunning = true;

        _receiver.RepeatAction(InputAudioAction, INPUT_AUDIO_ACTION_PERIOD);
    }

    /// <summary>
    /// This method is running as a separate task that enters <see cref="RealtimeSessionClient.SendInputAudio(Stream, CancellationToken)"/>
    /// and is running in a loop inside.
    /// </summary>
    /// <param name="cancellation"></param>
    private void SendAudioInputTask(CancellationToken cancellation)
    {
        if ((_internalAudioBuffer is not null) && (_audioCancellation is not null))
        {
            _receiver.SendInputAudio(_internalAudioBuffer.Output.Stream, _audioCancellation.Token);
        }
    }

    /// <summary>
    /// This method is invoked every <see cref="INPUT_AUDIO_ACTION_PERIOD"/> miliseconds.
    /// </summary>
    private void InputAudioAction()
    {
        if (_internalAudioInput is not null && _audioOutputFrames is not null &&
            _audioCancellation is not null && !_audioCancellation.IsCancellationRequested)
        {
            int framesRead = -1;
            short[] buffer = new short[AUDIO_INPUT_FRAME_CAPACITY];

            // Drain all currently available caller audio in bounded transfers.
            while (!_audioCancellation.IsCancellationRequested && 
                   _internalAudioInput.FreeCapacity >= AUDIO_INPUT_FRAME_CAPACITY &&
                   framesRead != 0)
            {
                framesRead = _audioOutputFrames.Read(
                    buffer,
                    0,
                    AUDIO_INPUT_FRAME_CAPACITY);
                if (framesRead > 0 && !_internalAudioInput.TryWrite(buffer, 0, framesRead))
                {
                    throw new InvalidOperationException(
                        "The internal Realtime audio buffer could not accept caller audio frames.");
                }
            }
        }
    }

    private void HandleSessionExceptions(Action sessionFunction)
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
            _info.Error("Conversation session failed.", ex);
            TaskExceptionOccurred(ex);
        }
    }

    /// <summary>
    /// Forwarded to <see cref="HandleEvent(object?,InputAudioTaskFinished)"/>.
    /// </summary>
    private void AudioInputFinished(object? s, TaskCompleted ev)
    {
        _receiver.SessionState.InputAudioRunning = false;
        InvokeConversationEvent(new InputAudioTaskFinished());
    }

    private void TaskExceptionOccurred(Exception ex)
    {
        InvokeConversationEvent(new TaskExceptionOccured(ex));
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
