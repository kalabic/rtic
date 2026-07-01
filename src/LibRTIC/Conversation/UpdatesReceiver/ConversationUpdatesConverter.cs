using OpenAI.Realtime;
using LibRTIC.MiniTaskLib;

namespace LibRTIC.Conversation.UpdatesReceiver;

#pragma warning disable OPENAI002

public class ConversationUpdatesConverter
{
    public ReplaceAndForward<ConversationSessionStarted, 
                             RealtimeServerUpdateSessionCreated> 
                             _actionSessionStarted;

    public ReplaceAndForward<ConversationInputAudioCleared, 
                             RealtimeServerUpdateInputAudioBufferCleared> 
                             _actionInputAudioCleared;

    public ReplaceAndForward<ConversationInputAudioCommitted, 
                             RealtimeServerUpdateInputAudioBufferCommitted> 
                             _actionInputAudioCommitted;

    public ReplaceAndForward<ConversationItemCreated, 
                             RealtimeServerUpdateConversationItemCreated> 
                             _actionItemCreated;

    public ReplaceAndForward<ConversationItemDeleted, 
                             RealtimeServerUpdateConversationItemDeleted> 
                             _actionItemDeleted;

    public ReplaceAndForward<ConversationError, 
                             RealtimeServerUpdateError> 
                             _actionError;

    public ReplaceAndForward<ConversationInputSpeechStarted, 
                             RealtimeServerUpdateInputAudioBufferSpeechStarted> 
                             _actionInputSpeechStarted;

    public ReplaceAndForward<ConversationInputSpeechFinished, 
                             RealtimeServerUpdateInputAudioBufferSpeechStopped> 
                             _actionInputSpeechFinished;

    public ReplaceAndForward<ConversationItemStreamingAudioFinished,
                             RealtimeServerUpdateResponseOutputAudioDone>
                             _actionItemStreamingAudioFinished;

    public ConvertAndForward<ConversationInputTranscriptionFailed, 
                             RealtimeServerUpdateConversationItemInputAudioTranscriptionFailed> 
                             _actionInputTranscriptionFailed;

    public ConvertAndForward<ConversationInputTranscriptionFinished, 
                             RealtimeServerUpdateConversationItemInputAudioTranscriptionCompleted> 
                             _actionInputTranscriptionFinished;

    public ReplaceAndForward<ConversationItemStreamingAudioTranscriptionFinished, 
                             ConversationItemStreamingAudioTranscriptionFinished> 
                             _actionItemStreamingAudioTranscriptionFinished;

    public ConvertAndForward<ConversationItemStreamingFinished, 
                             RealtimeServerUpdateResponseOutputItemDone> 
                             _actionItemStreamingFinished;

    public ConvertAndForward<ConversationItemStreamingAudioPartDelta,
                             RealtimeServerUpdateResponseOutputAudioDelta>
                             _actionItemStreamingPartAudioDelta;

    public ConvertAndForward<ConversationItemStreamingTranscriptionPartDelta,
                             RealtimeServerUpdateResponseOutputAudioTranscriptDelta>
                             _actionItemStreamingPartTranscriptDelta;

    public ReplaceAndForward<ConversationItemStreamingPartFinished,
                             RealtimeServerUpdateResponseContentPartDone>
                             _actionItemStreamingPartFinished;

    public ConvertAndForward<ConversationItemStreamingStarted, 
                             RealtimeServerUpdateResponseOutputItemAdded> 
                             _actionItemStreamingStarted;

    public ReplaceAndForward<ConversationItemStreamingTextFinished,
                             RealtimeServerUpdateResponseOutputTextDone>
                             _actionItemStreamingTextFinished;

    public ReplaceAndForward<ConversationRateLimits, 
                             ConversationRateLimits> 
                             _actionRateLimits;

    public ReplaceAndForward<ConversationResponseFinished, 
                             RealtimeServerUpdateResponseDone> 
                             _actionResponseFinished;

    public ReplaceAndForward<ConversationResponseStarted, 
                             RealtimeServerUpdateResponseCreated> 
                             _actionResponseStarted;

    public ReplaceAndForward<ConversationSessionConfigured, 
                             RealtimeServerUpdateSessionUpdated> 
                             _actionSessionConfigured;

    public ReplaceAndForward<ConversationItemTruncated, 
                             RealtimeServerUpdateConversationItemTruncated> 
                             _actionItemTruncated;

    public ConversationUpdatesConverter(EventProducerCollection sourceEvents, EventQueue eventQueue)
    {
        _actionSessionStarted = new(sourceEvents, eventQueue);
        _actionInputAudioCleared = new(sourceEvents, eventQueue);
        _actionInputAudioCommitted = new(sourceEvents, eventQueue);
        _actionItemCreated = new(sourceEvents, eventQueue);
        _actionItemDeleted = new(sourceEvents, eventQueue);
        _actionError = new(sourceEvents, eventQueue);
        _actionInputSpeechStarted = new(sourceEvents, eventQueue);
        _actionInputSpeechFinished = new(sourceEvents, eventQueue);
        _actionItemStreamingAudioFinished = new(sourceEvents, eventQueue);
        _actionInputTranscriptionFailed = new(sourceEvents, eventQueue,
            (update) => { return new ConversationInputTranscriptionFailedConverter(update); });
        _actionInputTranscriptionFinished = new(sourceEvents, eventQueue,
            (update) => { return new ConversationInputTranscriptionFinishedConverter(update); });
        _actionItemStreamingAudioTranscriptionFinished = new(sourceEvents, eventQueue);
        _actionItemStreamingFinished = new(sourceEvents, eventQueue,
            (update) => { return new ConversationItemStreamingFinishedConverter(update); });
        _actionItemStreamingPartAudioDelta = new(sourceEvents, eventQueue,
            (update) => { return new ConversationItemStreamingPartAudioDeltaConverter(update); });
        _actionItemStreamingPartTranscriptDelta = new(sourceEvents, eventQueue,
            (update) => { return new ConversationItemStreamingPartTranscriptDeltaConverter(update); });
        _actionItemStreamingPartFinished = new(sourceEvents, eventQueue);
        _actionItemStreamingStarted = new(sourceEvents, eventQueue,
            (update) => { return new ConversationItemStreamingStartedConverter(update); });
        _actionItemStreamingTextFinished = new(sourceEvents, eventQueue);
        _actionRateLimits = new(sourceEvents, eventQueue);
        _actionResponseFinished = new(sourceEvents, eventQueue);
        _actionResponseStarted = new(sourceEvents, eventQueue);
        _actionSessionConfigured = new(sourceEvents, eventQueue);
        _actionItemTruncated = new(sourceEvents, eventQueue);
    }


    private class ConversationInputTranscriptionFailedConverter(RealtimeServerUpdateConversationItemInputAudioTranscriptionFailed update)
        : ConversationInputTranscriptionFailed
    {
        private readonly string _errorMessage = update.Error.Message;

        public string ErrorMessage { get { return _errorMessage; } }

        public ConversationInputTranscriptionFailed Base() { return this; }
    }

    private class ConversationInputTranscriptionFinishedConverter(RealtimeServerUpdateConversationItemInputAudioTranscriptionCompleted update)
        : ConversationInputTranscriptionFinished
    {
        private readonly string _transcript = update.Transcript;

        public string Transcript { get { return _transcript; } }

        public ConversationInputTranscriptionFinished Base() { return this; }
    }

    private class ConversationItemStreamingPartAudioDeltaConverter(RealtimeServerUpdateResponseOutputAudioDelta update)
        : ConversationItemStreamingAudioPartDelta
    {
        private readonly BinaryData _audio = update.Delta;

        private readonly string _itemId = update.ItemId;

        public BinaryData Audio { get { return _audio; } }

        public string ItemId { get { return _itemId; } }

        public ConversationItemStreamingAudioPartDelta Base() { return this; }
    }

    private class ConversationItemStreamingPartTranscriptDeltaConverter(RealtimeServerUpdateResponseOutputAudioTranscriptDelta update)
        : ConversationItemStreamingTranscriptionPartDelta
    {
        private readonly string _itemId = update.ItemId;

        private readonly string _transcript = update.Delta;

        public string ItemId { get { return _itemId; } }

        public string Transcript { get { return _transcript; } }

        public ConversationItemStreamingTranscriptionPartDelta Base() { return this; }
    }

    private class ConversationItemStreamingStartedConverter(RealtimeServerUpdateResponseOutputItemAdded update)
        : ConversationItemStreamingStarted
    {
        private readonly string _functionName =
            update.Item is RealtimeFunctionCallItem functionCallItem ? functionCallItem.FunctionName : string.Empty;

        private readonly string _itemId =
            update.Item is RealtimeFunctionCallItem functionCallItem ? functionCallItem.Id : string.Empty;

        public string FunctionName { get { return _functionName; } }

        public string ItemId { get { return _itemId; } }

        public ConversationItemStreamingStarted Base() { return this; }
    }

    private class ConversationItemStreamingFinishedConverter(RealtimeServerUpdateResponseOutputItemDone update)
        : ConversationItemStreamingFinished
    {
        private readonly string _functionName =
            update.Item is RealtimeFunctionCallItem functionCallItem ? functionCallItem.FunctionName : string.Empty;

        private readonly string _itemId =
            update.Item is RealtimeFunctionCallItem functionCallItem ? functionCallItem.Id : string.Empty;

        public string FunctionName { get { return _functionName; } }

        public string ItemId { get { return _itemId; } }

        public ConversationItemStreamingFinished Base() { return this; }
    }
}
