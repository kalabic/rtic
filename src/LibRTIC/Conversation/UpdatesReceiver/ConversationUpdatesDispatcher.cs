using LibRTIC.MiniTaskLib.MessageQueue;
using LibRTIC.MiniTaskLib.Model;
using OpenAI.Realtime;
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

    protected void DispatchUpdate(RealtimeServerUpdate update)
    {
        if (update is RealtimeServerUpdateError errorUpdate)
        {
            _converter._actionError.ProcessNew();
        }
        else if (update is RealtimeServerUpdateInputAudioBufferCleared audioClearedUpdate)
        {
            _sessionState.nInputAudioCleared++;
            _sessionState.SpeechStarted = false;
            _sessionState.WaitingTranscription = false;
            _converter._actionInputAudioCleared.ProcessNew();
        }
        else if (update is RealtimeServerUpdateInputAudioBufferCommitted audioCommitedUpdate)
        {
            _converter._actionInputAudioCommitted.ProcessNew();
        }
        else if (update is RealtimeServerUpdateInputAudioBufferSpeechStarted speechStartedUpdate)
        {
            _sessionState.SpeechStarted = true;
            _converter._actionInputSpeechStarted.ProcessNew();
        }
        else if (update is RealtimeServerUpdateInputAudioBufferSpeechStopped speechStoppedUpdate)
        {
            _sessionState.SpeechStarted = false;
            _sessionState.WaitingTranscription = true;
            _converter._actionInputSpeechFinished.ProcessNew();
        }
        else if (update is RealtimeServerUpdateConversationItemInputAudioTranscriptionDelta transcriptionDeltaUpdate)
        {
            // TODO
        }
        else if (update is RealtimeServerUpdateConversationItemInputAudioTranscriptionFailed transcriptionFailedUpdate)
        {
            _sessionState.nTranscriptionFailed++;
            _sessionState.WaitingTranscription = false;
            _converter._actionInputTranscriptionFailed.Convert(transcriptionFailedUpdate);
        }
        else if (update is RealtimeServerUpdateConversationItemInputAudioTranscriptionCompleted transcriptionCompletedUpdate)
        {
            _sessionState.nTranscriptionFinished++;
            _sessionState.WaitingTranscription = false;
            _converter._actionInputTranscriptionFinished.Convert(transcriptionCompletedUpdate);
        }
        else if (update is RealtimeServerUpdateConversationItemAdded itemAddedUpdate)
        {
            // TODO
        }
        else if (update is RealtimeServerUpdateConversationItemCreated itemCreatedUpdate)
        {
            _converter._actionItemCreated.ProcessNew();
        }
        else if (update is RealtimeServerUpdateConversationItemDeleted itemDeletedUpdate)
        {
            _converter._actionItemDeleted.ProcessNew();
        }
        else if (update is RealtimeServerUpdateConversationItemDone itemDoneUpdate)
        {
            // TODO
        }
        else if (update is RealtimeServerUpdateResponseOutputAudioDone audioDoneUpdate)
        {
            _converter._actionItemStreamingAudioFinished.ProcessNew();
        }
        else if (update is RealtimeServerUpdateResponseOutputAudioTranscriptDone audioTranscriptionDoneUpdate)
        {
            _converter._actionItemStreamingAudioTranscriptionFinished.ProcessNew();
        }
        else if (update is RealtimeServerUpdateResponseOutputItemDone streamingDoneUpdate)
        {
            _sessionState.StreamingStarted = false;
            _converter._actionItemStreamingFinished.Convert(streamingDoneUpdate);
        }
        else if (update is RealtimeServerUpdateResponseOutputAudioDelta audioDeltaUpdate)
        {
            _converter._actionItemStreamingPartAudioDelta.Convert(audioDeltaUpdate);
        }
        else if (update is RealtimeServerUpdateResponseOutputAudioTranscriptDelta transcriptDeltaUpdate)
        {
            _converter._actionItemStreamingPartTranscriptDelta.Convert(transcriptDeltaUpdate);
        }
        else if (update is RealtimeServerUpdateResponseContentPartAdded contentPartAddedUpdate)
        {
            // TODO
        }
        else if (update is RealtimeServerUpdateResponseContentPartDone contentPartDoneUpdate)
        {
            _converter._actionItemStreamingPartFinished.ProcessNew();
        }
        else if (update is RealtimeServerUpdateResponseOutputItemAdded streamingStartedUpdate)
        {
            _sessionState.StreamingStarted = true;
            _converter._actionItemStreamingStarted.Convert(streamingStartedUpdate);
        }
        else if (update is RealtimeServerUpdateResponseOutputTextDone textFinishedUpdate)
        {
            _converter._actionItemStreamingTextFinished.ProcessNew();
        }
        else if (update is RealtimeServerUpdateConversationItemTruncated truncatedUpdate)
        {
            _converter._actionItemTruncated.ProcessNew();
        }
        else if (update is RealtimeServerUpdateRateLimitsUpdated rateLimitsUpdate)
        {
            _converter._actionRateLimits.ProcessNew();
        }
        else if (update is RealtimeServerUpdateResponseDone responseFinishedUpdate)
        {
            _sessionState.nResponseFinished++;
            _sessionState.ResponseStarted = false;
            _converter._actionResponseFinished.ProcessNew();
        }
        else if (update is RealtimeServerUpdateResponseCreated responseCreatedUpdate)
        {
            _sessionState.nResponseStarted++;
            _sessionState.ResponseStarted = true;
            _converter._actionResponseStarted.ProcessNew();
        }
        else if (update is RealtimeServerUpdateSessionUpdated sessionConfiguredUpdate)
        {
            _converter._actionSessionConfigured.ProcessNew();
        }
        else if (update is RealtimeServerUpdateSessionCreated sessionStartedUpdate)
        {
            _sessionState.SessionStarted = true;
            _converter._actionSessionStarted.ProcessNew();
        }
#if DEBUG
        else
        {
            throw new InvalidOperationException("Unhandled server update: " + update.GetType().Name);
        }
#endif
    }
}
