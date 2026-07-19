using OpenAI.Realtime;
using LibRTIC.MiniTaskLib;
using DotBase.Log;
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

    protected RealtimeSessionClient? _session = null;


    public ConversationUpdatesReceiver(InfoLog info)
        : this(info, CancellationToken.None) { }

    public ConversationUpdatesReceiver(InfoLog info, CancellationToken cancellation)
        : base(info)
    {
        this._cancellation = new ConversationCancellation(cancellation);

        _forwardedEvents.EnableInvokeFor<ClientStartedConnecting>();
        _forwardedEvents.EnableInvokeFor<InputAudioTaskFinished>();
        _forwardedEvents.EnableInvokeFor<FailedToConnect>();
    }

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

    public void SetSession(RealtimeSessionClient session)
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
            _session?.CancelResponse();
        });
    }

    public Task InterruptResponseAsync(CancellationToken cancellationToken)
    {
        RealtimeSessionClient session = GetConnectedSession();
        return session.CancelResponseAsync(cancellationToken);
    }

    public Task TruncateOutputItemAsync(
        string itemId,
        int contentIndex,
        TimeSpan audioEndTime,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        ArgumentOutOfRangeException.ThrowIfNegative(contentIndex);
        if (audioEndTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(audioEndTime));
        }

        RealtimeSessionClient session = GetConnectedSession();
        return session.TruncateItemAsync(
            itemId,
            contentIndex,
            audioEndTime,
            cancellationToken);
    }

    public Task StartResponseAsync(string? instructions, CancellationToken cancellationToken)
    {
        RealtimeSessionClient session = GetConnectedSession();

        return string.IsNullOrWhiteSpace(instructions)
            ? session.StartResponseAsync(cancellationToken)
            : session.StartResponseAsync(
                new RealtimeResponseOptions { Instructions = instructions },
                cancellationToken);
    }

    private RealtimeSessionClient GetConnectedSession()
    {
        RealtimeSessionClient session = _session
            ?? throw new InvalidOperationException("The Realtime conversation session has not been created.");

        if (!IsWebSocketOpen)
        {
            throw new InvalidOperationException("The Realtime conversation session is not connected.");
        }

        return session;
    }

    public void FinishReceiver()
    {
        if (_sessionState.receiverState == ConversationReceiverState.Connected)
        {
            _sessionState.receiverState = ConversationReceiverState.FinishAfterResponse;
            HandleSessionExceptions(() =>
            {
                _session?.CancelResponse();
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
                await foreach (RealtimeServerUpdate update in _session.ReceiveUpdatesAsync(_cancellation.WebSocketToken))
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
                await foreach (RealtimeServerUpdate update in _session.ReceiveUpdatesAsync(_cancellation.WebSocketToken))
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
