using OpenAI.Realtime;
using LibRTIC.MiniTaskLib;
using LibRTIC.MiniTaskLib.Model;
using System.Net.WebSockets;

namespace LibRTIC.Conversation.UpdatesReceiver;

#pragma warning disable OPENAI002

public class ConversationUpdatesReceiver : ConversationUpdatesDispatcher
{
    public ConversationReceiverState ReceiverState { get { return _sessionState.receiverState; } }

    /// <summary>
    /// Alias for <see cref="_forwardedEvents"/>, invoked from message queue thread.
    /// </summary>
    public EventQueue ReceiverEvents { get { return _forwardedEvents; } }

    public ConversationUpdatesInfo SessionState { get { return _sessionState; } }

    public ConversationCancellation Cancellation { get { return _cancellation; } }

    public bool IsWebSocketOpen { get { return (_session is not null) ? (_session.WebSocket.State == WebSocketState.Open) : false; } }


    protected ConversationCancellation _cancellation;

    protected RealtimeSession? _session = null;


    public ConversationUpdatesReceiver(Info info)
        : this(info, CancellationToken.None) { }

    public ConversationUpdatesReceiver(Info info, CancellationToken cancellation)
        : base(info)
    {
        this._cancellation = new ConversationCancellation(cancellation);

        _forwardedEvents.EnableInvokeFor<ClientStartedConnecting>();
        _forwardedEvents.EnableInvokeFor<InputAudioTaskFinished>();
        _forwardedEvents.EnableInvokeFor<FailedToConnect>();
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
            _cancellation.Dispose();
            _session?.Dispose();
        }

        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    public void SetSession(RealtimeSession session)
    {
        this._session = session;
    }

    public void SendInputAudio(Stream audio, CancellationToken cancellationToken)
    {
        HandleSessionExceptions(() =>
        {
            if (IsWebSocketOpen)
            {
                _session?.SendInputAudio(audio, cancellationToken);
            }
        });
    }

    public void ClearInputAudio()
    {
        HandleSessionExceptions(() =>
        {
            if (IsWebSocketOpen)
            {
                _session?.ClearInputAudio();
            }
        });
    }

    public void CancelMicrophone()
    {
        _cancellation.CancelMicrophone();
    }

    public void InterruptResponse()
    {
        HandleSessionExceptions(() =>
        {
            _session?.InterruptResponseAsync();
        });
    }

    public void FinishReceiver()
    {
        if (_sessionState.receiverState == ConversationReceiverState.Connected)
        {
            _sessionState.receiverState = ConversationReceiverState.FinishAfterResponse;
            HandleSessionExceptions(() =>
            {
                _session?.InterruptResponseAsync();
            });
        }
    }

    public void ReceiveUpdates(CancellationToken cancellation)
    {
        var task = HandleSessionExceptionsAsync(async () =>
        {
            if (_session is not null)
            {
                _sessionState.receiverState = ConversationReceiverState.Connected;
                await foreach (RealtimeUpdate update in _session.ReceiveUpdatesAsync(_cancellation.WebSocketToken))
                {
                    if (!DispatchAndProcess(update))
                    {
                        break;
                    }
                }
                _sessionState.receiverState = ConversationReceiverState.Disconnected;
            }
        });

        HandleSessionExceptions( () => task.Wait() );
        InvokeEvent(new ConversationSessionFinished());
    }

    protected async Task ReceiveUpdatesAsync()
    {
        _sessionState.receiverState = ConversationReceiverState.Connected;
        await HandleSessionExceptionsAsync(async () =>
        {
            if (_session is not null)
            {
                await foreach (RealtimeUpdate update in _session.ReceiveUpdatesAsync(_cancellation.WebSocketToken))
                {
                    if (!DispatchAndProcess(update))
                    {
                        break;
                    }
                }
            }
        });
        _sessionState.receiverState = ConversationReceiverState.Disconnected;
        InvokeEvent(new ConversationSessionFinished());
    }

    private bool DispatchAndProcess(RealtimeUpdate update)
    {
        DispatchUpdate(update);

        // Normal state, continue receiving updates as usual.
        if (_sessionState.receiverState == ConversationReceiverState.Connected)
        {
            return true;
        }

        if (_sessionState.receiverState == ConversationReceiverState.FinishAfterResponse)
        {
            if (_sessionState.ResponseStarted)
            {
                return true;
            }

            _sessionState.receiverState = ConversationReceiverState.Disconnecting;
        }

        if (_sessionState.receiverState == ConversationReceiverState.Disconnecting)
        {
            if (IsWebSocketOpen)
            {
                HandleSessionExceptions(() =>
                {
                    var socket = _session?.WebSocket;
                    if (socket != null)
                    {
                        _ = socket.CloseOutputAsync(
                            WebSocketCloseStatus.NormalClosure, null, _cancellation.WebSocketToken);
                    }
                });
                return true;
            }
            else
            {
                // If socket is closed, return false to break the receiver loop.
                return false;
            }
        }

        return true;
    }
}
