using LibRTIC.MiniTaskLib;

namespace LibRTIC.Conversation.UpdatesReceiver;


/// <summary>
/// Coordinates non-blocking cancellation requests for conversation token sources and
/// defers their disposal until asynchronously executing cancellation callbacks complete.
/// </summary>
public class ConversationCancellation : CancellationSet
{
    /// <summary>
    /// Observes only main shell cancellation state. Does not care if only network or audio channels are cancelled.
    /// </summary>
    public bool IsCancellationRequested { get { return _shellCanceler.IsCancellationRequested; } }

    public CancellationToken ShellToken { get { return _shellCanceler.Token; } }

    public CancellationToken SpeechToken { get { return _speechCanceler.Token; } }

    public CancellationToken MicrophoneToken { get { return _microphoneCanceler.Token; } }

    public CancellationToken WebSocketToken { get { return _webSocketCanceler.Token; } }


    protected CancellationTokenSource _shellCanceler;

    protected CancellationTokenSource _speechCanceler;

    protected CancellationTokenSource _microphoneCanceler;

    protected CancellationTokenSource _webSocketCanceler;


    public ConversationCancellation()
    {
        _shellCanceler = RegisterCancellationSource(new CancellationTokenSource());
        _speechCanceler = RegisterCancellationSource(new CancellationTokenSource());
        _microphoneCanceler = RegisterCancellationSource(
            CancellationTokenSource.CreateLinkedTokenSource(_shellCanceler.Token));
        _webSocketCanceler = RegisterCancellationSource(new CancellationTokenSource());
    }

    public ConversationCancellation(CancellationToken externalToken)
    {
        _shellCanceler = RegisterCancellationSource(
            CancellationTokenSource.CreateLinkedTokenSource(externalToken));
        _speechCanceler = RegisterCancellationSource(new CancellationTokenSource());
        _microphoneCanceler = RegisterCancellationSource(
            CancellationTokenSource.CreateLinkedTokenSource(_shellCanceler.Token));
        _webSocketCanceler = RegisterCancellationSource(new CancellationTokenSource());
    }

    public void CancelMicrophone()
    {
        RequestCancellation(_microphoneCanceler);
    }

    public void CancelWebSocket()
    {
        RequestCancellation(_webSocketCanceler);
    }

    /// <summary>
    /// Requests cancellation of conversation work without running registered
    /// cancellation callbacks on the calling thread. The WebSocket remains available
    /// for a graceful closing handshake; call <see cref="CancelWebSocket"/> to force it.
    /// </summary>
    public void CancelConversation()
    {
        RequestCancellation([
            _shellCanceler,
            _speechCanceler,
            _microphoneCanceler
        ]);
    }
}
