using OpenAI.RealtimeConversation;
using LibRTIC.MiniTaskLib.MessageQueue;
using LibRTIC.MiniTaskLib.Model;
using System.Net.WebSockets;

namespace LibRTIC.Conversation.UpdatesReceiver;

#pragma warning disable OPENAI002


/// <summary>
/// This class starts additional task to invoke 'forwarded event handlers' that do not
/// hang on network updates fetcher task when it invokes event with an update.
/// </summary>
public abstract class ConversationUpdatesDispatcher : ForwardedEventQueue
{
    protected ConversationUpdatesInfo _sessionState = new();

    private ConversationUpdatesConverter _converter;

    protected ConversationUpdatesDispatcher(Info info)
        : this(info, CancellationToken.None) { }

    protected ConversationUpdatesDispatcher(Info info, CancellationToken cancellation)
        : base(info)
    {
        _converter = new(_events, _forwardedEvents);
        // Local session notifications
        ForwardEventTo< ConversationSessionFinished >(_forwardedEvents);
    }

    override protected void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            _sessionState.Disposed = true;
        }

        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    protected void HandleSessionExceptions(Action sessionFunction)
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
            NotifyExceptionOccurred(ex);
        }
        catch (Exception ex)
        {
            NotifyExceptionOccurred(ex);
        }
    }

    public async Task HandleSessionExceptionsAsync(Func<Task> sessionFunctionAsync)
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
            NotifyExceptionOccurred(ex);
        }
    }

    protected void DispatchUpdate(ConversationUpdate update)
    {
        if (update is ConversationErrorUpdate errorUpdate)
        {
            _converter._actionError.ProcessNew();
        }
        else if (update is ConversationInputAudioClearedUpdate audioClearedUpdate)
        {
            _sessionState.nInputAudioCleared++;
            _sessionState.SpeechStarted = false;
            _sessionState.WaitingTranscription = false;
            _converter._actionInputAudioCleared.ProcessNew();
        }
        else if (update is ConversationInputAudioCommittedUpdate audioCommitedUpdate)
        {
            _converter._actionInputAudioCommitted.ProcessNew();
        }
        else if (update is ConversationInputSpeechStartedUpdate speechStartedUpdate)
        {
            _sessionState.SpeechStarted = true;
            _converter._actionInputSpeechStarted.ProcessNew();
        }
        else if (update is ConversationInputSpeechFinishedUpdate speechFinishedUpdate)
        {
            _sessionState.SpeechStarted = false;
            _sessionState.WaitingTranscription = true;
            _converter._actionInputSpeechFinished.ProcessNew();
        }
        else if (update is ConversationInputTranscriptionFailedUpdate transcriptionFailedUpdate)
        {
            _sessionState.nTranscriptionFailed++;
            _sessionState.WaitingTranscription = false;
            _converter._actionInputTranscriptionFailed.Convert(transcriptionFailedUpdate);
        }
        else if (update is ConversationInputTranscriptionFinishedUpdate transcriptionFinishedUpdate)
        {
            _sessionState.nTranscriptionFinished++;
            _sessionState.WaitingTranscription = false;
            _converter._actionInputTranscriptionFinished.Convert(transcriptionFinishedUpdate);
        }
        else if (update is ConversationItemCreatedUpdate itemCreatedUpdate)
        {
            _converter._actionItemCreated.ProcessNew();
        }
        else if (update is ConversationItemDeletedUpdate itemDeletedUpdate)
        {
            _converter._actionItemDeleted.ProcessNew();
        }
        else if (update is ConversationItemStreamingAudioFinishedUpdate audioFinishedUpdate)
        {
            _converter._actionItemStreamingAudioFinished.ProcessNew();
        }
        else if (update is ConversationItemStreamingAudioTranscriptionFinishedUpdate audioTranscriptionFinishedUpdate)
        {
            _converter._actionItemStreamingAudioTranscriptionFinished.ProcessNew();
        }
        else if (update is ConversationItemStreamingFinishedUpdate streamingFinishedUpdate)
        {
            _sessionState.StreamingStarted = false;
            _converter._actionItemStreamingFinished.Convert(streamingFinishedUpdate);
        }
        else if (update is ConversationItemStreamingPartDeltaUpdate deltaUpdate)
        {
            _converter._actionItemStreamingPartDelta.Convert(deltaUpdate);
        }
        else if (update is ConversationItemStreamingPartFinishedUpdate partFinishedUpdate)
        {
            _converter._actionItemStreamingPartFinished.ProcessNew();
        }
        else if (update is ConversationItemStreamingStartedUpdate streamingStartedUpdate)
        {
            _sessionState.StreamingStarted = true;
            _converter._actionItemStreamingStarted.Convert(streamingStartedUpdate);
        }
        else if (update is ConversationItemStreamingTextFinishedUpdate textFinishedUpdate)
        {
            _converter._actionItemStreamingTextFinished.ProcessNew();
        }
        else if (update is ConversationItemTruncatedUpdate truncatedUpdate)
        {
            _converter._actionItemTruncated.ProcessNew();
        }
        else if (update is ConversationRateLimitsUpdate rateLimitsUpdate)
        {
            _converter._actionRateLimits.ProcessNew();
        }
        else if (update is ConversationResponseFinishedUpdate responseFinishedUpdate)
        {
            _sessionState.nResponseFinished++;
            _sessionState.ResponseStarted = false;
            _converter._actionResponseFinished.ProcessNew();
        }
        else if (update is ConversationResponseStartedUpdate responseStartedUpdate)
        {
            _sessionState.nResponseStarted++;
            _sessionState.ResponseStarted = true;
            _converter._actionResponseStarted.ProcessNew();
        }
        else if (update is ConversationSessionConfiguredUpdate sessionConfiguredUpdate)
        {
            _converter._actionSessionConfigured.ProcessNew();
        }
        else if (update is ConversationSessionStartedUpdate sessionStartedUpdate)
        {
            _sessionState.SessionStarted = true;
            _converter._actionSessionStarted.ProcessNew();
        }
    }
}
