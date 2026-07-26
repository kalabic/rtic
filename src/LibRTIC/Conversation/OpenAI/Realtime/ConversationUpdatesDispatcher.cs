using System.Net.WebSockets;
using DotBase.Log;
using LibRTIC.Conversation.OpenAI;
using LibRTIC.Conversation.UpdatesReceiver;
using LibRTIC.MiniTaskLib.MessageQueue;
using OpenAI.Realtime;

namespace LibRTIC.Conversation.OpenAI.Realtime;

#pragma warning disable OPENAI002

/// <summary>
/// Translates provider updates and starts a mailbox so application handlers do
/// not run on the network receiver task.
/// </summary>
internal abstract class ConversationUpdatesDispatcher : EventMailbox
{
    internal static IReadOnlySet<Type> SupportedUpdateTypes =>
        OpenAISessionEventTranslator.SupportedUpdateTypes;

    protected ConversationUpdatesInfo _sessionState = new();

    private readonly ConversationUpdatesConverter _converter;
    private readonly OpenAISessionEventTranslator _translator;

    protected ConversationUpdatesDispatcher(InfoLog info)
        : this(info, CancellationToken.None) { }

    protected ConversationUpdatesDispatcher(InfoLog info, CancellationToken cancellation)
        : base(info)
    {
        _translator = new();
        _converter = new(_events, _forwardedEvents);

        ForwardEventTo<ConversationSessionFinished>(_forwardedEvents);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sessionState.Disposed = true;
        }

        base.Dispose(disposing);
    }

    protected void HandleSessionExceptions(
        Action sessionFunction,
        CancellationToken cancellation)
    {
        try
        {
            sessionFunction();
        }
        catch (WebSocketException ex)
        {
            NotifyExceptionOccurred(ex);
        }
        catch (OperationCanceledException ex)
        {
            if (!cancellation.IsCancellationRequested)
            {
                NotifyExceptionOccurred(ex);
            }
        }
        catch (Exception ex)
        {
            NotifyExceptionOccurred(ex);
        }
    }

    protected async Task HandleSessionExceptionsAsync(
        Func<Task> sessionFunctionAsync,
        CancellationToken cancellation)
    {
        try
        {
            await sessionFunctionAsync();
        }
        catch (WebSocketException ex)
        {
            NotifyExceptionOccurred(ex);
        }
        catch (OperationCanceledException ex)
        {
            if (!cancellation.IsCancellationRequested)
            {
                NotifyExceptionOccurred(ex);
            }
        }
        catch (Exception ex)
        {
            NotifyExceptionOccurred(ex);
        }
    }

    protected void DispatchUpdate(RealtimeServerUpdate providerUpdate)
    {
        RTICSessionEvent update = _translator.Translate(providerUpdate);
        UpdateSessionState(update);
        _converter.Forward(update);
    }

    private void UpdateSessionState(RTICSessionEvent update)
    {
        switch (update)
        {
            case RTICInputTranscriptionCompleted:
                _sessionState.nTranscriptionFinished++;
                DecrementIfPositive(ref _sessionState.PendingTranscriptionCount);
                break;
            case RTICInputTranscriptionFailed:
                _sessionState.nTranscriptionFailed++;
                DecrementIfPositive(ref _sessionState.PendingTranscriptionCount);
                break;
            case RTICInputAudioCleared:
                _sessionState.nInputAudioCleared++;
                _sessionState.ActiveSpeechCount = 0;
                _sessionState.PendingTranscriptionCount = 0;
                break;
            case RTICInputSpeechStarted:
                _sessionState.nSpeechStarted++;
                _sessionState.ActiveSpeechCount++;
                break;
            case RTICInputSpeechFinished:
                _sessionState.nSpeechFinished++;
                DecrementIfPositive(ref _sessionState.ActiveSpeechCount);
                _sessionState.PendingTranscriptionCount++;
                break;
            case RTICResponseStarted:
                _sessionState.nResponseStarted++;
                _sessionState.ActiveResponseCount++;
                break;
            case RTICResponseCompleted:
                _sessionState.nResponseFinished++;
                DecrementIfPositive(ref _sessionState.ActiveResponseCount);
                break;
            case RTICOutputItemStarted:
                _sessionState.nStreamingStarted++;
                _sessionState.ActiveStreamingItemCount++;
                break;
            case RTICOutputItemCompleted:
                _sessionState.nStreamingFinished++;
                DecrementIfPositive(ref _sessionState.ActiveStreamingItemCount);
                break;
            case RTICSessionCreated:
                _sessionState.SessionStarted = true;
                break;
        }
    }

    private static void DecrementIfPositive(ref int value)
    {
        if (value > 0)
        {
            value--;
        }
    }
}

#pragma warning restore OPENAI002
