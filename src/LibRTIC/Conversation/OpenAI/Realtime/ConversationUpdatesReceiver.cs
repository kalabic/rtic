using OpenAI.Realtime;
using LibRTIC.MiniTaskLib;
using DotBase.Log;
using System.Net.WebSockets;
using System.Diagnostics;
using LibRTIC.Conversation.UpdatesReceiver;
using LibRTIC.Config;
using AudioFormatLib.IO;
using LibRTIC.Conversation.Control;

namespace LibRTIC.Conversation.OpenAI.Realtime;

#pragma warning disable OPENAI002

internal abstract class ConversationUpdatesReceiver : ConversationUpdatesDispatcher, RTICUpdatesReceiver
{
    public ConversationReceiverState ReceiverState { get { return _sessionState.receiverState; } }

    /// <summary>
    /// Alias for <see cref="_dispatchedEvents"/>, invoked from the action queue thread.
    /// </summary>
    public EventQueue ReceiverEvents { get { return _dispatchedEvents; } }

    public ConversationUpdatesInfo SessionState { get { return _sessionState; } }

    public ConversationCancellation Cancellation { get { return _cancellation; } }

    public bool IsWebSocketOpen { get { return (_session is not null) ? (_session.WebSocket.State == WebSocketState.Open) : false; } }


    protected ConversationCancellation _cancellation;

    protected RealtimeSessionClient? _session = null;

    private readonly ConversationCommandDispatcher _commands;

    /// <summary>
    /// Creates an instance unbound to any external cancellation source.
    /// </summary>
    protected ConversationUpdatesReceiver(InfoLog info)
        : this(info, new ConversationCancellation(CancellationToken.None)) { }


    /// <summary>
    /// Creates an instance cancellable by external cancellation source.
    /// </summary>
    protected ConversationUpdatesReceiver(InfoLog info, ConversationCancellation cancellation)
        : base(info)
    {
        SetLabel("Conversation Updates Receiver");

        _cancellation = cancellation;
        _commands = new ConversationCommandDispatcher(
            info,
            cancellation.WebSocketToken);
        _dispatchedEvents.EnableInvokeFor<ClientStartedConnecting>();
        _dispatchedEvents.EnableInvokeFor<InputAudioTaskFinished>();
        _dispatchedEvents.EnableInvokeFor<FailedToConnectMsg>();
    }

#if DEBUG_FINALIZER
    ~ConversationUpdatesReceiver()
    {
        _info.Info("~ConversationUpdatesReceiver()");
    }
#endif

    override protected void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            _commands.Dispose();
            _cancellation.Dispose();
            _session?.Dispose();
        }

        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    protected void SetSession(RealtimeSessionClient session)
    {
        this._session = session;
        _commands.Start(new OpenAIRealtimeCommandTransport(session));
    }

    protected void SendInputAudioStream(Stream audio, CancellationToken cancellationToken)
    {
        HandleSessionExceptions(() =>
        {
            if (IsWebSocketOpen)
            {
                _session?.SendInputAudio(audio, cancellationToken);
            }
        }, cancellationToken);
    }

    protected void ClearInputAudio()
    {
        HandleSessionExceptions(() =>
        {
            if (IsWebSocketOpen)
            {
                _session?.ClearInputAudio();
            }
        }, _cancellation.WebSocketToken);
    }

    public Task RequestResponseAsync(
        RTICResponseRequest request,
        CancellationToken cancellationToken)
        => _commands.RequestResponseAsync(request, cancellationToken);

    public Task InterruptOutputAsync(
        RTICOutputInterruption request,
        CancellationToken cancellationToken)
        => _commands.InterruptOutputAsync(request, cancellationToken);

    public virtual void FinishReceiver()
    {
        _cancellation.CancelMicrophone();

        if (_sessionState.receiverState == ConversationReceiverState.Connected)
        {
            _commands.BeginShutdown(
                () => _sessionState.receiverState =
                    ConversationReceiverState.FinishAfterResponse);
        }
    }

    protected override void ObserveSessionEvent(RTICSessionEvent update)
        => _commands.Observe(update);

    public void ReceiveUpdates(CancellationToken cancellation)
    {
        var task = HandleSessionExceptionsAsync(async () =>
        {
            if (_session is not null)
            {
                _sessionState.receiverState = ConversationReceiverState.Connected;
                await foreach (RealtimeServerUpdate update in _session.ReceiveUpdatesAsync(_cancellation.WebSocketToken))
                {
                    if (!DispatchAndProcess(update))
                    {
                        break;
                    }
                }
            }
        }, _cancellation.WebSocketToken);
        HandleSessionExceptions( () => task.Wait(), _cancellation.WebSocketToken);

        Debug.Assert(_session == null || (_session.WebSocket.State != WebSocketState.Open && _session.WebSocket.State != WebSocketState.CloseSent));
        Debug.Assert(_sessionState.receiverState == ConversationReceiverState.Disconnecting);
        _sessionState.receiverState = ConversationReceiverState.Disconnected;
        InvokeEvent(new ConversationSessionFinished());
    }

    protected async Task ReceiveUpdatesAsync()
    {
        _sessionState.receiverState = ConversationReceiverState.Connected;
        await HandleSessionExceptionsAsync(async () =>
        {
            if (_session is not null)
            {
                await foreach (RealtimeServerUpdate update in _session.ReceiveUpdatesAsync(_cancellation.WebSocketToken))
                {
                    if (!DispatchAndProcess(update))
                    {
                        break;
                    }
                }
            }
        }, _cancellation.WebSocketToken);

        Debug.Assert(_session == null || (_session.WebSocket.State != WebSocketState.Open && _session.WebSocket.State != WebSocketState.CloseSent));
        Debug.Assert(_sessionState.receiverState == ConversationReceiverState.Disconnecting);
        _sessionState.receiverState = ConversationReceiverState.Disconnected;
        InvokeEvent(new ConversationSessionFinished());
    }

    private bool DispatchAndProcess(RealtimeServerUpdate update)
    {
        DispatchUpdate(update);

        // Normal state, continue receiving updates as usual.
        if (_sessionState.receiverState == ConversationReceiverState.Connected)
        {
            return true;
        }

        if (_sessionState.receiverState == ConversationReceiverState.FinishAfterResponse)
        {
            if (_sessionState.ActiveResponseCount > 0)
            {
                return true;
            }

            _sessionState.receiverState = ConversationReceiverState.Disconnecting;
        }

        if (_sessionState.receiverState == ConversationReceiverState.Disconnecting)
        {
            // Note to reviewer: Correct shutdown of client WebSocket inside RealtimeSessionClient expects its
            // ReceiveUpdatesAsync() to be continuously invoked until it reaches state 'WebSocketState.Closed'.
            WebSocket? socket = _session?.WebSocket;

            if (socket is null)
            {
                return false;
            }

            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                socket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure, null, _cancellation.WebSocketToken)
                    .GetAwaiter().GetResult();
            }

            return socket.State == WebSocketState.CloseSent;
        }

        return true;
    }

    public abstract void ConfigureWith(RTICConfig options);
    public abstract void SendInputAudio(IAudioStreamOutput stream, CancellationToken cancellation);
}
