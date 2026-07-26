using DotBase.Event;
using LibRTIC.MiniTaskLib;

namespace LibRTIC.Conversation.UpdatesReceiver;

internal sealed class ConversationUpdatesConverter
{
    private readonly NeutralForwarder<RTICSessionCreated> _sessionCreated;
    private readonly NeutralForwarder<RTICSessionConfigured> _sessionConfigured;
    private readonly NeutralForwarder<RTICTimelineCreated> _timelineCreated;
    private readonly NeutralForwarder<RTICItemAdded> _itemAdded;
    private readonly NeutralForwarder<RTICItemCreated> _itemCreated;
    private readonly NeutralForwarder<RTICItemCompleted> _itemCompleted;
    private readonly NeutralForwarder<RTICItemRetrieved> _itemRetrieved;
    private readonly NeutralForwarder<RTICItemDeleted> _itemDeleted;
    private readonly NeutralForwarder<RTICItemTruncated> _itemTruncated;
    private readonly NeutralForwarder<RTICInputAudioCleared> _inputAudioCleared;
    private readonly NeutralForwarder<RTICInputAudioCommitted> _inputAudioCommitted;
    private readonly NeutralForwarder<RTICInputAudioTimedOut> _inputAudioTimedOut;
    private readonly NeutralForwarder<RTICInputSpeechStarted> _inputSpeechStarted;
    private readonly NeutralForwarder<RTICInputSpeechFinished> _inputSpeechFinished;
    private readonly NeutralForwarder<RTICDtmfReceived> _dtmfReceived;
    private readonly NeutralForwarder<RTICInputTranscriptionDelta> _transcriptionDelta;
    private readonly NeutralForwarder<RTICInputTranscriptionCompleted> _transcriptionCompleted;
    private readonly NeutralForwarder<RTICInputTranscriptionFailed> _transcriptionFailed;
    private readonly NeutralForwarder<RTICInputTranscriptionSegment> _transcriptionSegment;
    private readonly NeutralForwarder<RTICResponseStarted> _responseStarted;
    private readonly NeutralForwarder<RTICResponseCompleted> _responseCompleted;
    private readonly NeutralForwarder<RTICOutputItemStarted> _outputItemStarted;
    private readonly NeutralForwarder<RTICOutputItemCompleted> _outputItemCompleted;
    private readonly NeutralForwarder<RTICOutputContentPartStarted> _contentPartStarted;
    private readonly NeutralForwarder<RTICOutputContentPartCompleted> _contentPartCompleted;
    private readonly NeutralForwarder<RTICOutputAudioDelta> _audioDelta;
    private readonly NeutralForwarder<RTICOutputAudioCompleted> _audioCompleted;
    private readonly NeutralForwarder<RTICOutputTextDelta> _textDelta;
    private readonly NeutralForwarder<RTICOutputTextCompleted> _textCompleted;
    private readonly NeutralForwarder<RTICOutputTranscriptDelta> _transcriptDelta;
    private readonly NeutralForwarder<RTICOutputTranscriptCompleted> _transcriptCompleted;
    private readonly NeutralForwarder<RTICFunctionArgumentsDelta> _functionArgumentsDelta;
    private readonly NeutralForwarder<RTICFunctionArgumentsCompleted> _functionArgumentsCompleted;
    private readonly NeutralForwarder<RTICMcpToolsListed> _mcpToolsListed;
    private readonly NeutralForwarder<RTICMcpCallStarted> _mcpCallStarted;
    private readonly NeutralForwarder<RTICMcpCallArgumentsDelta> _mcpArgumentsDelta;
    private readonly NeutralForwarder<RTICMcpCallArgumentsCompleted> _mcpArgumentsCompleted;
    private readonly NeutralForwarder<RTICMcpCallCompleted> _mcpCallCompleted;
    private readonly NeutralForwarder<RTICMcpCallFailed> _mcpCallFailed;
    private readonly NeutralForwarder<RTICErrorReceived> _errorReceived;
    private readonly NeutralForwarder<RTICRateLimitsUpdated> _rateLimitsUpdated;
    private readonly NeutralForwarder<RTICOutputAudioPlaybackStarted> _playbackStarted;
    private readonly NeutralForwarder<RTICOutputAudioPlaybackCompleted> _playbackCompleted;
    private readonly NeutralForwarder<RTICOutputAudioPlaybackCleared> _playbackCleared;
    private readonly NeutralForwarder<RTICUnknownProviderEvent> _unknownProviderEvent;

    internal ConversationUpdatesConverter(
        EventProducerCollection sourceEvents,
        EventQueue eventQueue)
    {
        _sessionCreated = new(sourceEvents, eventQueue);
        _sessionConfigured = new(sourceEvents, eventQueue);
        _timelineCreated = new(sourceEvents, eventQueue);
        _itemAdded = new(sourceEvents, eventQueue);
        _itemCreated = new(sourceEvents, eventQueue);
        _itemCompleted = new(sourceEvents, eventQueue);
        _itemRetrieved = new(sourceEvents, eventQueue);
        _itemDeleted = new(sourceEvents, eventQueue);
        _itemTruncated = new(sourceEvents, eventQueue);
        _inputAudioCleared = new(sourceEvents, eventQueue);
        _inputAudioCommitted = new(sourceEvents, eventQueue);
        _inputAudioTimedOut = new(sourceEvents, eventQueue);
        _inputSpeechStarted = new(sourceEvents, eventQueue);
        _inputSpeechFinished = new(sourceEvents, eventQueue);
        _dtmfReceived = new(sourceEvents, eventQueue);
        _transcriptionDelta = new(sourceEvents, eventQueue);
        _transcriptionCompleted = new(sourceEvents, eventQueue);
        _transcriptionFailed = new(sourceEvents, eventQueue);
        _transcriptionSegment = new(sourceEvents, eventQueue);
        _responseStarted = new(sourceEvents, eventQueue);
        _responseCompleted = new(sourceEvents, eventQueue);
        _outputItemStarted = new(sourceEvents, eventQueue);
        _outputItemCompleted = new(sourceEvents, eventQueue);
        _contentPartStarted = new(sourceEvents, eventQueue);
        _contentPartCompleted = new(sourceEvents, eventQueue);
        _audioDelta = new(sourceEvents, eventQueue);
        _audioCompleted = new(sourceEvents, eventQueue);
        _textDelta = new(sourceEvents, eventQueue);
        _textCompleted = new(sourceEvents, eventQueue);
        _transcriptDelta = new(sourceEvents, eventQueue);
        _transcriptCompleted = new(sourceEvents, eventQueue);
        _functionArgumentsDelta = new(sourceEvents, eventQueue);
        _functionArgumentsCompleted = new(sourceEvents, eventQueue);
        _mcpToolsListed = new(sourceEvents, eventQueue);
        _mcpCallStarted = new(sourceEvents, eventQueue);
        _mcpArgumentsDelta = new(sourceEvents, eventQueue);
        _mcpArgumentsCompleted = new(sourceEvents, eventQueue);
        _mcpCallCompleted = new(sourceEvents, eventQueue);
        _mcpCallFailed = new(sourceEvents, eventQueue);
        _errorReceived = new(sourceEvents, eventQueue);
        _rateLimitsUpdated = new(sourceEvents, eventQueue);
        _playbackStarted = new(sourceEvents, eventQueue);
        _playbackCompleted = new(sourceEvents, eventQueue);
        _playbackCleared = new(sourceEvents, eventQueue);
        _unknownProviderEvent = new(sourceEvents, eventQueue);
    }

    internal void Forward(RTICSessionEvent update)
    {
        switch (update)
        {
            case RTICSessionCreated value: _sessionCreated.Forward(value); break;
            case RTICSessionConfigured value: _sessionConfigured.Forward(value); break;
            case RTICTimelineCreated value: _timelineCreated.Forward(value); break;
            case RTICItemAdded value: _itemAdded.Forward(value); break;
            case RTICItemCreated value: _itemCreated.Forward(value); break;
            case RTICItemCompleted value: _itemCompleted.Forward(value); break;
            case RTICItemRetrieved value: _itemRetrieved.Forward(value); break;
            case RTICItemDeleted value: _itemDeleted.Forward(value); break;
            case RTICItemTruncated value: _itemTruncated.Forward(value); break;
            case RTICInputAudioCleared value: _inputAudioCleared.Forward(value); break;
            case RTICInputAudioCommitted value: _inputAudioCommitted.Forward(value); break;
            case RTICInputAudioTimedOut value: _inputAudioTimedOut.Forward(value); break;
            case RTICInputSpeechStarted value: _inputSpeechStarted.Forward(value); break;
            case RTICInputSpeechFinished value: _inputSpeechFinished.Forward(value); break;
            case RTICDtmfReceived value: _dtmfReceived.Forward(value); break;
            case RTICInputTranscriptionDelta value: _transcriptionDelta.Forward(value); break;
            case RTICInputTranscriptionCompleted value: _transcriptionCompleted.Forward(value); break;
            case RTICInputTranscriptionFailed value: _transcriptionFailed.Forward(value); break;
            case RTICInputTranscriptionSegment value: _transcriptionSegment.Forward(value); break;
            case RTICResponseStarted value: _responseStarted.Forward(value); break;
            case RTICResponseCompleted value: _responseCompleted.Forward(value); break;
            case RTICOutputItemStarted value: _outputItemStarted.Forward(value); break;
            case RTICOutputItemCompleted value: _outputItemCompleted.Forward(value); break;
            case RTICOutputContentPartStarted value: _contentPartStarted.Forward(value); break;
            case RTICOutputContentPartCompleted value: _contentPartCompleted.Forward(value); break;
            case RTICOutputAudioDelta value: _audioDelta.Forward(value); break;
            case RTICOutputAudioCompleted value: _audioCompleted.Forward(value); break;
            case RTICOutputTextDelta value: _textDelta.Forward(value); break;
            case RTICOutputTextCompleted value: _textCompleted.Forward(value); break;
            case RTICOutputTranscriptDelta value: _transcriptDelta.Forward(value); break;
            case RTICOutputTranscriptCompleted value: _transcriptCompleted.Forward(value); break;
            case RTICFunctionArgumentsDelta value: _functionArgumentsDelta.Forward(value); break;
            case RTICFunctionArgumentsCompleted value: _functionArgumentsCompleted.Forward(value); break;
            case RTICMcpToolsListed value: _mcpToolsListed.Forward(value); break;
            case RTICMcpCallStarted value: _mcpCallStarted.Forward(value); break;
            case RTICMcpCallArgumentsDelta value: _mcpArgumentsDelta.Forward(value); break;
            case RTICMcpCallArgumentsCompleted value: _mcpArgumentsCompleted.Forward(value); break;
            case RTICMcpCallCompleted value: _mcpCallCompleted.Forward(value); break;
            case RTICMcpCallFailed value: _mcpCallFailed.Forward(value); break;
            case RTICErrorReceived value: _errorReceived.Forward(value); break;
            case RTICRateLimitsUpdated value: _rateLimitsUpdated.Forward(value); break;
            case RTICOutputAudioPlaybackStarted value: _playbackStarted.Forward(value); break;
            case RTICOutputAudioPlaybackCompleted value: _playbackCompleted.Forward(value); break;
            case RTICOutputAudioPlaybackCleared value: _playbackCleared.Forward(value); break;
            case RTICUnknownProviderEvent value: _unknownProviderEvent.Forward(value); break;
            default:
                throw new NotSupportedException(
                    $"No neutral forwarder is registered for '{update.GetType().FullName}'.");
        }
    }

    private sealed class NeutralForwarder<TEvent>
        where TEvent : RTICSessionEvent
    {
        private readonly EventContainer<TEvent>? _event;

        internal NeutralForwarder(
            EventProducerCollection sourceEvents,
            EventQueue eventQueue)
        {
            _event = eventQueue.ForwardFrom<TEvent>(sourceEvents);
        }

        internal void Forward(TEvent update) => _event?.Invoke(null, update);
    }
}
