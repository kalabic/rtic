using OpenAI.RealtimeConversation;
using LibRTIC.MiniTaskLib;

namespace LibRTIC.Conversation.UpdatesReceiver;

#pragma warning disable OPENAI002

public class ConversationUpdatesConverter
{
    public ReplaceAndForward<ConversationSessionStarted, 
                             ConversationSessionStartedUpdate> 
                             _actionSessionStarted;

    public ReplaceAndForward<ConversationInputAudioCleared, 
                             ConversationInputAudioClearedUpdate> 
                             _actionInputAudioCleared;

    public ReplaceAndForward<ConversationInputAudioCommitted, 
                             ConversationInputAudioCommittedUpdate> 
                             _actionInputAudioCommitted;

    public ReplaceAndForward<ConversationItemCreated, 
                             ConversationItemCreatedUpdate> 
                             _actionItemCreated;

    public ReplaceAndForward<ConversationItemDeleted, 
                             ConversationItemDeletedUpdate> 
                             _actionItemDeleted;

    public ReplaceAndForward<ConversationError, 
                             ConversationErrorUpdate> 
                             _actionError;

    public ReplaceAndForward<ConversationInputSpeechStarted, 
                             ConversationInputSpeechStartedUpdate> 
                             _actionInputSpeechStarted;

    public ReplaceAndForward<ConversationInputSpeechFinished, 
                             ConversationInputSpeechFinishedUpdate> 
                             _actionInputSpeechFinished;

    public ReplaceAndForward<ConversationItemStreamingAudioFinished, 
                             ConversationItemStreamingAudioFinishedUpdate> 
                             _actionItemStreamingAudioFinished;

    public ConvertAndForward<ConversationInputTranscriptionFailed, 
                             ConversationInputTranscriptionFailedUpdate> 
                             _actionInputTranscriptionFailed;

    public ConvertAndForward<ConversationInputTranscriptionFinished, 
                             ConversationInputTranscriptionFinishedUpdate> 
                             _actionInputTranscriptionFinished;

    public ReplaceAndForward<ConversationItemStreamingAudioTranscriptionFinished, 
                             ConversationItemStreamingAudioTranscriptionFinished> 
                             _actionItemStreamingAudioTranscriptionFinished;

    public ConvertAndForward<ConversationItemStreamingFinished, 
                             ConversationItemStreamingFinishedUpdate> 
                             _actionItemStreamingFinished;

    public ConvertAndForward<ConversationItemStreamingPartDelta, 
                             ConversationItemStreamingPartDeltaUpdate> 
                             _actionItemStreamingPartDelta;

    public ReplaceAndForward<ConversationItemStreamingPartFinished, 
                             ConversationItemStreamingPartFinishedUpdate> 
                             _actionItemStreamingPartFinished;

    public ConvertAndForward<ConversationItemStreamingStarted, 
                             ConversationItemStreamingStartedUpdate> 
                             _actionItemStreamingStarted;

    public ReplaceAndForward<ConversationItemStreamingTextFinished, 
                             ConversationItemStreamingTextFinishedUpdate> 
                             _actionItemStreamingTextFinished;

    public ReplaceAndForward<ConversationRateLimits, 
                             ConversationRateLimits> 
                             _actionRateLimits;

    public ReplaceAndForward<ConversationResponseFinished, 
                             ConversationResponseFinishedUpdate> 
                             _actionResponseFinished;

    public ReplaceAndForward<ConversationResponseStarted, 
                             ConversationResponseStartedUpdate> 
                             _actionResponseStarted;

    public ReplaceAndForward<ConversationSessionConfigured, 
                             ConversationSessionConfiguredUpdate> 
                             _actionSessionConfigured;

    public ReplaceAndForward<ConversationItemTruncated, 
                             ConversationItemTruncatedUpdate> 
                             _actionItemTruncated;

    public ConversationUpdatesConverter(EventCollection eventCollection, EventQueue eventQueue)
    {
        _actionSessionStarted = new(eventCollection, eventQueue);
        _actionInputAudioCleared = new(eventCollection, eventQueue);
        _actionInputAudioCommitted = new(eventCollection, eventQueue);
        _actionItemCreated = new(eventCollection, eventQueue);
        _actionItemDeleted = new(eventCollection, eventQueue);
        _actionError = new(eventCollection, eventQueue);
        _actionInputSpeechStarted = new(eventCollection, eventQueue);
        _actionInputSpeechFinished = new(eventCollection, eventQueue);
        _actionItemStreamingAudioFinished = new(eventCollection, eventQueue);
        _actionInputTranscriptionFailed = new(eventCollection, eventQueue,
            (update) => { return new ConversationInputTranscriptionFailedConverter(update); });
        _actionInputTranscriptionFinished = new(eventCollection, eventQueue,
            (update) => { return new ConversationInputTranscriptionFinishedConverter(update); });
        _actionItemStreamingAudioTranscriptionFinished = new(eventCollection, eventQueue);
        _actionItemStreamingFinished = new(eventCollection, eventQueue,
            (update) => { return new ConversationItemStreamingFinishedConverter(update); });
        _actionItemStreamingPartDelta = new(eventCollection, eventQueue,
            (update) => { return new ConversationItemStreamingPartDeltaConverter(update); });
        _actionItemStreamingPartFinished = new(eventCollection, eventQueue);
        _actionItemStreamingStarted = new(eventCollection, eventQueue,
            (update) => { return new ConversationItemStreamingStartedConverter(update); });
        _actionItemStreamingTextFinished = new(eventCollection, eventQueue);
        _actionRateLimits = new(eventCollection, eventQueue);
        _actionResponseFinished = new(eventCollection, eventQueue);
        _actionResponseStarted = new(eventCollection, eventQueue);
        _actionSessionConfigured = new(eventCollection, eventQueue);
        _actionItemTruncated = new(eventCollection, eventQueue);
    }


    private class ConversationInputTranscriptionFailedConverter(ConversationInputTranscriptionFailedUpdate update)
        : ConversationInputTranscriptionFailed
    {
        private readonly string _errorMessage = update.ErrorMessage;

        public string ErrorMessage { get { return _errorMessage; } }

        public ConversationInputTranscriptionFailed Base() { return this; }
    }

    private class ConversationInputTranscriptionFinishedConverter(ConversationInputTranscriptionFinishedUpdate update)
        : ConversationInputTranscriptionFinished
    {
        private readonly string _transcript = update.Transcript;

        public string Transcript { get { return _transcript; } }

        public ConversationInputTranscriptionFinished Base() { return this; }
    }

    private class ConversationItemStreamingPartDeltaConverter(ConversationItemStreamingPartDeltaUpdate update)
        : ConversationItemStreamingPartDelta
    {
        private readonly BinaryData _audio = update.AudioBytes;

        private readonly string _itemId = update.ItemId;

        private readonly string _transcript = update.AudioTranscript;

        public BinaryData Audio { get { return _audio; } }

        public string ItemId { get { return _itemId; } }

        public string Transcript { get { return _transcript; } }

        public ConversationItemStreamingPartDelta Base() { return this; }
    }

    private class ConversationItemStreamingStartedConverter(ConversationItemStreamingStartedUpdate update)
        : ConversationItemStreamingStarted
    {
        private readonly string _functionName = update.FunctionName;

        private readonly string _itemId = update.ItemId;

        public string FunctionName { get { return _functionName; } }

        public string ItemId { get { return _itemId; } }

        public ConversationItemStreamingStarted Base() { return this; }
    }

    private class ConversationItemStreamingFinishedConverter(ConversationItemStreamingFinishedUpdate update)
        : ConversationItemStreamingFinished
    {
        private readonly string _functionName = update.FunctionName;

        private readonly string _itemId = update.ItemId;

        public string FunctionName { get { return _functionName; } }

        public string ItemId { get { return _itemId; } }

        public ConversationItemStreamingFinished Base() { return this; }
    }
}
