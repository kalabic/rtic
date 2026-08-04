using AudioFormatLib;
using LibRTIC.Realtime;

namespace LibRTIC.Conversation;

public interface IRTICResponseEvent
{
    string ResponseId { get; }
}

public interface IRTICItemEvent
{
    string ItemId { get; }
}

public interface IRTICOutputItemEvent : IRTICResponseEvent, IRTICItemEvent
{
    int OutputIndex { get; }
}

public interface IRTICContentEvent : IRTICOutputItemEvent
{
    int ContentIndex { get; }
}

public abstract record RTICSessionEvent(RTICEventId EventId);

public sealed record RTICSessionCreated(
    RTICSessionInfo Session) : RTICSessionEvent(RTICEventId.SessionCreated);

public sealed record RTICSessionConfigured(
    RTICSessionInfo Session) : RTICSessionEvent(RTICEventId.SessionConfigured);

/// <summary>
/// Reports creation of the provider-managed ordered timeline that contains
/// conversation items.
/// </summary>
public sealed record RTICTimelineCreated(
    string TimelineId) : RTICSessionEvent(RTICEventId.TimelineCreated);

public sealed record RTICItemAdded(
    RTICItem Item,
    string? PreviousItemId) : RTICSessionEvent(RTICEventId.ItemAdded), IRTICItemEvent
{
    public string ItemId => Item.Id;
}

public sealed record RTICItemCreated(
    RTICItem Item,
    string? PreviousItemId) : RTICSessionEvent(RTICEventId.ItemCreated), IRTICItemEvent
{
    public string ItemId => Item.Id;
}

public sealed record RTICItemCompleted(
    RTICItem Item,
    string? PreviousItemId) : RTICSessionEvent(RTICEventId.ItemCompleted), IRTICItemEvent
{
    public string ItemId => Item.Id;
}

public sealed record RTICItemRetrieved(
    RTICItem Item) : RTICSessionEvent(RTICEventId.ItemRetrieved), IRTICItemEvent
{
    public string ItemId => Item.Id;
}

public sealed record RTICItemDeleted(
    string ItemId) : RTICSessionEvent(RTICEventId.ItemDeleted), IRTICItemEvent;

public sealed record RTICItemTruncated(
    string ItemId,
    int ContentIndex,
    TimeSpan AudioEndTime) : RTICSessionEvent(RTICEventId.ItemTruncated), IRTICItemEvent;

public sealed record RTICInputAudioCleared()
    : RTICSessionEvent(RTICEventId.InputAudioCleared);

public sealed record RTICInputAudioCommitted(
    string ItemId,
    string? PreviousItemId) : RTICSessionEvent(RTICEventId.InputAudioCommitted), IRTICItemEvent;

public sealed record RTICInputAudioTimedOut(
    string ItemId,
    TimeSpan AudioStartTime,
    TimeSpan AudioEndTime) : RTICSessionEvent(RTICEventId.InputAudioTimedOut), IRTICItemEvent;

public sealed record RTICInputSpeechStarted(
    string ItemId,
    TimeSpan AudioStartTime) : RTICSessionEvent(RTICEventId.InputSpeechStarted), IRTICItemEvent;

public sealed record RTICInputSpeechFinished(
    string ItemId,
    TimeSpan AudioEndTime) : RTICSessionEvent(RTICEventId.InputSpeechFinished), IRTICItemEvent;

public sealed record RTICDtmfReceived(
    string KeypadValue,
    DateTimeOffset ProviderReceivedAt) : RTICSessionEvent(RTICEventId.DtmfReceived);

public sealed record RTICInputTranscriptionDelta : RTICSessionEvent, IRTICItemEvent
{
    public RTICInputTranscriptionDelta(
        string itemId,
        int? contentIndex,
        string delta,
        IEnumerable<RTICLogProbability> logProbabilities)
        : base(RTICEventId.InputTranscriptionDelta)
    {
        ItemId = itemId;
        ContentIndex = contentIndex;
        Delta = delta;
        LogProbabilities = RTICImmutable.Copy(logProbabilities);
    }

    public string ItemId { get; }

    public int? ContentIndex { get; }

    public string Delta { get; }

    public IReadOnlyList<RTICLogProbability> LogProbabilities { get; }
}

public sealed record RTICInputTranscriptionCompleted : RTICSessionEvent, IRTICItemEvent
{
    public RTICInputTranscriptionCompleted(
        string itemId,
        int contentIndex,
        string transcript,
        RTICTranscriptionUsage? usage,
        IEnumerable<RTICLogProbability> logProbabilities)
        : base(RTICEventId.InputTranscriptionCompleted)
    {
        ItemId = itemId;
        ContentIndex = contentIndex;
        Transcript = transcript;
        Usage = usage;
        LogProbabilities = RTICImmutable.Copy(logProbabilities);
    }

    public string ItemId { get; }

    public int ContentIndex { get; }

    public string Transcript { get; }

    public RTICTranscriptionUsage? Usage { get; }

    public IReadOnlyList<RTICLogProbability> LogProbabilities { get; }
}

public sealed record RTICInputTranscriptionFailed(
    string ItemId,
    int ContentIndex,
    RTICError Error) : RTICSessionEvent(RTICEventId.InputTranscriptionFailed), IRTICItemEvent
{
    public string ErrorMessage => Error.Message ?? string.Empty;
}

public sealed record RTICInputTranscriptionSegment(
    string ItemId,
    int ContentIndex,
    string SegmentId,
    float Start,
    float End,
    string Speaker,
    string Text) : RTICSessionEvent(RTICEventId.InputTranscriptionSegment), IRTICItemEvent;

public sealed record RTICResponseStarted(
    RTICResponse Response) : RTICSessionEvent(RTICEventId.ResponseStarted), IRTICResponseEvent
{
    public string ResponseId => Response.Id;
}

public sealed record RTICResponseCompleted(
    RTICResponse Response) : RTICSessionEvent(RTICEventId.ResponseCompleted), IRTICResponseEvent
{
    public string ResponseId => Response.Id;

    public bool IsCompleted => Response.Status == RTICResponseStatus.Completed;
}

public sealed record RTICOutputItemStarted(
    string ResponseId,
    RTICItem Item,
    int OutputIndex) : RTICSessionEvent(RTICEventId.OutputItemStarted), IRTICOutputItemEvent
{
    public string ItemId => Item.Id;
}

public sealed record RTICOutputItemCompleted(
    string ResponseId,
    RTICItem Item,
    int OutputIndex) : RTICSessionEvent(RTICEventId.OutputItemCompleted), IRTICOutputItemEvent
{
    public string ItemId => Item.Id;
}

public sealed record RTICOutputContentPartStarted(
    string ResponseId,
    string ItemId,
    int OutputIndex,
    int ContentIndex,
    RTICContentPart Part) : RTICSessionEvent(RTICEventId.OutputContentPartStarted), IRTICContentEvent;

public sealed record RTICOutputContentPartCompleted(
    string ResponseId,
    string ItemId,
    int OutputIndex,
    int ContentIndex,
    RTICContentPart Part) : RTICSessionEvent(RTICEventId.OutputContentPartCompleted), IRTICContentEvent;

public sealed record RTICOutputAudioDelta : RTICSessionEvent, IRTICContentEvent
{
    public RTICOutputAudioDelta(
        string responseId,
        string itemId,
        int outputIndex,
        int contentIndex,
        AudioPacket audio)
        : base(RTICEventId.OutputAudioDelta)
    {
        RealtimeAudioContract.ValidatePacket(in audio, nameof(audio));
        ResponseId = responseId;
        ItemId = itemId;
        OutputIndex = outputIndex;
        ContentIndex = contentIndex;
        Audio = audio;
    }

    public string ResponseId { get; }

    public string ItemId { get; }

    public int OutputIndex { get; }

    public int ContentIndex { get; }

    /// <summary>
    /// Mutable packet storage owned by this event and shared by shallow packet copies.
    /// </summary>
    public AudioPacket Audio { get; }
}

public sealed record RTICOutputAudioCompleted(
    string ResponseId,
    string ItemId,
    int OutputIndex,
    int ContentIndex) : RTICSessionEvent(RTICEventId.OutputAudioCompleted), IRTICContentEvent;

public sealed record RTICOutputTextDelta(
    string ResponseId,
    string ItemId,
    int OutputIndex,
    int ContentIndex,
    string Delta) : RTICSessionEvent(RTICEventId.OutputTextDelta), IRTICContentEvent;

public sealed record RTICOutputTextCompleted(
    string ResponseId,
    string ItemId,
    int OutputIndex,
    int ContentIndex,
    string Text) : RTICSessionEvent(RTICEventId.OutputTextCompleted), IRTICContentEvent;

public sealed record RTICOutputTranscriptDelta(
    string ResponseId,
    string ItemId,
    int OutputIndex,
    int ContentIndex,
    string Delta) : RTICSessionEvent(RTICEventId.OutputTranscriptDelta), IRTICContentEvent;

public sealed record RTICOutputTranscriptCompleted(
    string ResponseId,
    string ItemId,
    int OutputIndex,
    int ContentIndex,
    string Transcript) : RTICSessionEvent(RTICEventId.OutputTranscriptCompleted), IRTICContentEvent;

public sealed record RTICFunctionArgumentsDelta : RTICSessionEvent, IRTICOutputItemEvent
{
    public RTICFunctionArgumentsDelta(
        string responseId,
        string itemId,
        int outputIndex,
        string callId,
        ReadOnlyMemory<byte> delta)
        : base(RTICEventId.FunctionArgumentsDelta)
    {
        ResponseId = responseId;
        ItemId = itemId;
        OutputIndex = outputIndex;
        CallId = callId;
        Delta = RTICImmutable.Copy(delta);
    }

    public string ResponseId { get; }

    public string ItemId { get; }

    public int OutputIndex { get; }

    public string CallId { get; }

    public ReadOnlyMemory<byte> Delta { get; }
}

public sealed record RTICFunctionArgumentsCompleted : RTICSessionEvent, IRTICOutputItemEvent
{
    public RTICFunctionArgumentsCompleted(
        string responseId,
        string itemId,
        int outputIndex,
        string callId,
        string functionName,
        ReadOnlyMemory<byte> arguments)
        : base(RTICEventId.FunctionArgumentsCompleted)
    {
        ResponseId = responseId;
        ItemId = itemId;
        OutputIndex = outputIndex;
        CallId = callId;
        FunctionName = functionName;
        Arguments = RTICImmutable.Copy(arguments);
    }

    public string ResponseId { get; }

    public string ItemId { get; }

    public int OutputIndex { get; }

    public string CallId { get; }

    public string FunctionName { get; }

    public ReadOnlyMemory<byte> Arguments { get; }
}

public sealed record RTICMcpToolsListed(
    string ItemId,
    RTICMcpToolsListStatus Status) : RTICSessionEvent(RTICEventId.McpToolsListed), IRTICItemEvent;

public sealed record RTICMcpCallStarted(
    string ItemId,
    int OutputIndex) : RTICSessionEvent(RTICEventId.McpCallStarted), IRTICItemEvent;

public sealed record RTICMcpCallArgumentsDelta : RTICSessionEvent, IRTICOutputItemEvent
{
    public RTICMcpCallArgumentsDelta(
        string responseId,
        string itemId,
        int outputIndex,
        ReadOnlyMemory<byte> delta,
        string? obfuscation)
        : base(RTICEventId.McpCallArgumentsDelta)
    {
        ResponseId = responseId;
        ItemId = itemId;
        OutputIndex = outputIndex;
        Delta = RTICImmutable.Copy(delta);
        Obfuscation = obfuscation;
    }

    public string ResponseId { get; }

    public string ItemId { get; }

    public int OutputIndex { get; }

    public ReadOnlyMemory<byte> Delta { get; }

    public string? Obfuscation { get; }
}

public sealed record RTICMcpCallArgumentsCompleted : RTICSessionEvent, IRTICOutputItemEvent
{
    public RTICMcpCallArgumentsCompleted(
        string responseId,
        string itemId,
        int outputIndex,
        ReadOnlyMemory<byte> arguments)
        : base(RTICEventId.McpCallArgumentsCompleted)
    {
        ResponseId = responseId;
        ItemId = itemId;
        OutputIndex = outputIndex;
        Arguments = RTICImmutable.Copy(arguments);
    }

    public string ResponseId { get; }

    public string ItemId { get; }

    public int OutputIndex { get; }

    public ReadOnlyMemory<byte> Arguments { get; }
}

public sealed record RTICMcpCallCompleted(
    string ItemId,
    int OutputIndex) : RTICSessionEvent(RTICEventId.McpCallCompleted), IRTICItemEvent;

public sealed record RTICMcpCallFailed(
    string ItemId,
    int OutputIndex) : RTICSessionEvent(RTICEventId.McpCallFailed), IRTICItemEvent;

public sealed record RTICErrorReceived(
    RTICError Error) : RTICSessionEvent(RTICEventId.ErrorReceived);

public sealed record RTICRateLimitsUpdated : RTICSessionEvent
{
    public RTICRateLimitsUpdated(
        IEnumerable<RTICRateLimit> rateLimits)
        : base(RTICEventId.RateLimitsUpdated)
    {
        RateLimits = RTICImmutable.Copy(rateLimits);
    }

    public IReadOnlyList<RTICRateLimit> RateLimits { get; }
}

public sealed record RTICOutputAudioPlaybackStarted(
    string ResponseId) : RTICSessionEvent(RTICEventId.OutputAudioPlaybackStarted), IRTICResponseEvent;

public sealed record RTICOutputAudioPlaybackCompleted(
    string ResponseId) : RTICSessionEvent(RTICEventId.OutputAudioPlaybackCompleted), IRTICResponseEvent;

public sealed record RTICOutputAudioPlaybackCleared(
    string ResponseId) : RTICSessionEvent(RTICEventId.OutputAudioPlaybackCleared), IRTICResponseEvent;

public sealed record RTICUnknownProviderEvent(
    string ProviderName,
    string ProviderEventKind,
    string? DiagnosticMessage = null) : RTICSessionEvent(RTICEventId.Unknown);
