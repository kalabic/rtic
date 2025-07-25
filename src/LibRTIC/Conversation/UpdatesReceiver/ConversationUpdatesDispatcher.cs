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
#if DEBUG
        SetLabel("Updates Dispatcher");
#endif

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

    protected void DispatchUpdate(RealtimeUpdate update)
    {
        if (update is RealtimeErrorUpdate errorUpdate)
        {
            _converter._actionError.ProcessNew();
        }
        else if (update is InputAudioClearedUpdate audioClearedUpdate)
        {
            _sessionState.nInputAudioCleared++;
            _sessionState.SpeechStarted = false;
            _sessionState.WaitingTranscription = false;
            _converter._actionInputAudioCleared.ProcessNew();
        }
        else if (update is InputAudioCommittedUpdate audioCommitedUpdate)
        {
            _converter._actionInputAudioCommitted.ProcessNew();
        }
        else if (update is InputAudioSpeechStartedUpdate speechStartedUpdate)
        {
            _sessionState.SpeechStarted = true;
            _converter._actionInputSpeechStarted.ProcessNew();
        }
        else if (update is InputAudioSpeechFinishedUpdate speechFinishedUpdate)
        {
            _sessionState.SpeechStarted = false;
            _sessionState.WaitingTranscription = true;
            _converter._actionInputSpeechFinished.ProcessNew();
        }
        else if (update is InputAudioTranscriptionFailedUpdate transcriptionFailedUpdate)
        {
            _sessionState.nTranscriptionFailed++;
            _sessionState.WaitingTranscription = false;
            _converter._actionInputTranscriptionFailed.Convert(transcriptionFailedUpdate);
        }
        else if (update is InputAudioTranscriptionFinishedUpdate transcriptionFinishedUpdate)
        {
            _sessionState.nTranscriptionFinished++;
            _sessionState.WaitingTranscription = false;
            _converter._actionInputTranscriptionFinished.Convert(transcriptionFinishedUpdate);
        }
        else if (update is ItemCreatedUpdate itemCreatedUpdate)
        {
            _converter._actionItemCreated.ProcessNew();
        }
        else if (update is ItemDeletedUpdate itemDeletedUpdate)
        {
            _converter._actionItemDeleted.ProcessNew();
        }
        else if (update is OutputAudioFinishedUpdate audioFinishedUpdate)
        {
            _converter._actionItemStreamingAudioFinished.ProcessNew();
        }
        else if (update is OutputAudioTranscriptionFinishedUpdate audioTranscriptionFinishedUpdate)
        {
            _converter._actionItemStreamingAudioTranscriptionFinished.ProcessNew();
        }
        else if (update is OutputStreamingFinishedUpdate streamingFinishedUpdate)
        {
            _sessionState.StreamingStarted = false;
            _converter._actionItemStreamingFinished.Convert(streamingFinishedUpdate);
        }
        else if (update is OutputDeltaUpdate deltaUpdate)
        {
            _converter._actionItemStreamingPartDelta.Convert(deltaUpdate);
        }
        else if (update is OutputPartFinishedUpdate partFinishedUpdate)
        {
            _converter._actionItemStreamingPartFinished.ProcessNew();
        }
        else if (update is OutputStreamingStartedUpdate streamingStartedUpdate)
        {
            _sessionState.StreamingStarted = true;
            _converter._actionItemStreamingStarted.Convert(streamingStartedUpdate);
        }
        else if (update is OutputTextFinishedUpdate textFinishedUpdate)
        {
            _converter._actionItemStreamingTextFinished.ProcessNew();
        }
        else if (update is ItemTruncatedUpdate truncatedUpdate)
        {
            _converter._actionItemTruncated.ProcessNew();
        }
        else if (update is RateLimitsUpdate rateLimitsUpdate)
        {
            _converter._actionRateLimits.ProcessNew();
        }
        else if (update is ResponseFinishedUpdate responseFinishedUpdate)
        {
            _sessionState.nResponseFinished++;
            _sessionState.ResponseStarted = false;
            _converter._actionResponseFinished.ProcessNew();
        }
        else if (update is ResponseStartedUpdate responseStartedUpdate)
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
