using System.Collections.ObjectModel;

namespace LibRTIC.Conversation;

public enum RTICResponseStatus
{
    Unknown = 0,
    InProgress,
    Completed,
    Cancelled,
    Failed,
    Incomplete,
}

public enum RTICTranscriptionUsageKind
{
    Unknown = 0,
    Tokens,
    Duration,
}

public enum RTICMcpToolsListStatus
{
    InProgress = 0,
    Completed,
    Failed,
}

public abstract record RTICItem
{
    protected RTICItem(string id, string? status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        Status = status;
    }

    public string Id { get; }

    public string? Status { get; }
}

public sealed record RTICMessageItem : RTICItem
{
    public RTICMessageItem(
        string id,
        string? status,
        string? role,
        IEnumerable<RTICContentPart> content)
        : base(id, status)
    {
        Role = role;
        Content = RTICImmutable.Copy(content);
    }

    public string? Role { get; }

    public IReadOnlyList<RTICContentPart> Content { get; }

    public bool HasOutputAudio => Content.OfType<RTICAudioContentPart>()
        .Any(static part => !part.IsInput);

    public bool HasInputAudio => Content.OfType<RTICAudioContentPart>()
        .Any(static part => part.IsInput);
}

public sealed record RTICFunctionCallItem : RTICItem
{
    public RTICFunctionCallItem(
        string id,
        string? status,
        string? functionName,
        string? callId,
        ReadOnlyMemory<byte>? arguments)
        : base(id, status)
    {
        FunctionName = functionName;
        CallId = callId;
        Arguments = RTICImmutable.Copy(arguments);
    }

    public string? FunctionName { get; }

    public string? CallId { get; }

    public ReadOnlyMemory<byte>? Arguments { get; }
}

public sealed record RTICFunctionCallOutputItem : RTICItem
{
    public RTICFunctionCallOutputItem(
        string id,
        string? status,
        string? callId,
        string? output)
        : base(id, status)
    {
        CallId = callId;
        Output = output;
    }

    public string? CallId { get; }

    public string? Output { get; }
}

public sealed record RTICMcpCallItem : RTICItem
{
    public RTICMcpCallItem(
        string id,
        string? serverLabel,
        string? toolName,
        ReadOnlyMemory<byte>? arguments,
        string? output,
        RTICError? error,
        string? approvalRequestId)
        : base(id, null)
    {
        ServerLabel = serverLabel;
        ToolName = toolName;
        Arguments = RTICImmutable.Copy(arguments);
        Output = output;
        Error = error;
        ApprovalRequestId = approvalRequestId;
    }

    public string? ServerLabel { get; }

    public string? ToolName { get; }

    public ReadOnlyMemory<byte>? Arguments { get; }

    public string? Output { get; }

    public RTICError? Error { get; }

    public string? ApprovalRequestId { get; }
}

public sealed record RTICMcpApprovalRequestItem : RTICItem
{
    public RTICMcpApprovalRequestItem(
        string id,
        string? serverLabel,
        string? toolName,
        ReadOnlyMemory<byte>? arguments)
        : base(id, null)
    {
        ServerLabel = serverLabel;
        ToolName = toolName;
        Arguments = RTICImmutable.Copy(arguments);
    }

    public string? ServerLabel { get; }

    public string? ToolName { get; }

    public ReadOnlyMemory<byte>? Arguments { get; }
}

public sealed record RTICMcpApprovalResponseItem : RTICItem
{
    public RTICMcpApprovalResponseItem(
        string id,
        string? approvalRequestId,
        bool approved,
        string? reason)
        : base(id, null)
    {
        ApprovalRequestId = approvalRequestId;
        Approved = approved;
        Reason = reason;
    }

    public string? ApprovalRequestId { get; }

    public bool Approved { get; }

    public string? Reason { get; }
}

public sealed record RTICMcpListToolsItem : RTICItem
{
    public RTICMcpListToolsItem(
        string id,
        string? serverLabel,
        IEnumerable<RTICMcpTool> tools)
        : base(id, null)
    {
        ServerLabel = serverLabel;
        Tools = RTICImmutable.Copy(tools);
    }

    public string? ServerLabel { get; }

    public IReadOnlyList<RTICMcpTool> Tools { get; }
}

public sealed record RTICUnknownItem : RTICItem
{
    public RTICUnknownItem(
        string id,
        string? status,
        string kindName,
        IReadOnlyDictionary<string, string?>? metadata = null)
        : base(id, status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kindName);
        KindName = kindName;
        Metadata = RTICImmutable.Copy(metadata);
    }

    public string KindName { get; }

    public IReadOnlyDictionary<string, string?> Metadata { get; }
}

public abstract record RTICContentPart;

public sealed record RTICTextContentPart(string? Text, bool IsInput) : RTICContentPart;

public sealed record RTICAudioContentPart : RTICContentPart
{
    public RTICAudioContentPart(
        ReadOnlyMemory<byte>? audio,
        string? transcript,
        bool isInput)
    {
        Audio = audio.HasValue
            ? new ReadOnlyMemory<byte>(audio.Value.ToArray())
            : (ReadOnlyMemory<byte>?)null;
        Transcript = transcript;
        IsInput = isInput;
    }

    public ReadOnlyMemory<byte>? Audio { get; }

    public string? Transcript { get; }

    public bool IsInput { get; }
}

public sealed record RTICUnknownContentPart(
    string KindName,
    string? Text = null,
    string? Transcript = null,
    bool HasAudio = false) : RTICContentPart;

public sealed record RTICMcpTool(
    string? Name,
    string? Description,
    string? InputSchema,
    string? Annotations);

public sealed record RTICResponse
{
    public RTICResponse(
        string id,
        RTICResponseStatus status,
        IEnumerable<RTICItem> outputItems,
        IEnumerable<string> outputModalities,
        IReadOnlyDictionary<string, string?>? metadata = null,
        RTICUsage? usage = null,
        RTICResponseStatusDetails? statusDetails = null,
        string? timelineId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        Status = status;
        OutputItems = RTICImmutable.Copy(outputItems);
        OutputModalities = RTICImmutable.Copy(outputModalities);
        Metadata = RTICImmutable.Copy(metadata);
        Usage = usage;
        StatusDetails = statusDetails;
        TimelineId = timelineId;
    }

    public string Id { get; }

    public RTICResponseStatus Status { get; }

    public IReadOnlyList<RTICItem> OutputItems { get; }

    public IReadOnlyList<string> OutputModalities { get; }

    public IReadOnlyDictionary<string, string?> Metadata { get; }

    public RTICUsage? Usage { get; }

    public RTICResponseStatusDetails? StatusDetails { get; }

    public string? TimelineId { get; }
}

public sealed record RTICUsage(
    int? TotalTokenCount,
    int? InputTokenCount,
    int? OutputTokenCount,
    int? InputTextTokenCount = null,
    int? InputAudioTokenCount = null,
    int? InputImageTokenCount = null,
    int? CachedInputTokenCount = null,
    int? OutputTextTokenCount = null,
    int? OutputAudioTokenCount = null);

public sealed record RTICTranscriptionUsage(
    RTICTranscriptionUsageKind Kind,
    int? TotalTokenCount = null,
    int? InputTokenCount = null,
    int? OutputTokenCount = null,
    TimeSpan? Duration = null);

public sealed record RTICError(
    string? Code,
    string? Message,
    string? Parameter,
    string? RelatedEventId,
    string? Kind);

public sealed record RTICRateLimit(
    string? Name,
    int? Limit,
    int? RemainingCount,
    TimeSpan? TimeUntilReset);

public sealed record RTICLogProbability
{
    public RTICLogProbability(
        string? token,
        float logProbability,
        ReadOnlyMemory<byte> utf8Bytes)
    {
        Token = token;
        LogProbability = logProbability;
        Utf8Bytes = RTICImmutable.Copy(utf8Bytes);
    }

    public string? Token { get; }

    public float LogProbability { get; }

    public ReadOnlyMemory<byte> Utf8Bytes { get; }
}

public sealed record RTICSessionInfo
{
    public RTICSessionInfo(
        string? model,
        string? instructions,
        IEnumerable<string> outputModalities,
        string? maximumOutputTokens)
    {
        Model = model;
        Instructions = instructions;
        OutputModalities = RTICImmutable.Copy(outputModalities);
        MaximumOutputTokens = maximumOutputTokens;
    }

    public string? Model { get; }

    public string? Instructions { get; }

    public IReadOnlyList<string> OutputModalities { get; }

    public string? MaximumOutputTokens { get; }
}

public sealed record RTICResponseStatusDetails(
    string? Kind,
    string? Reason,
    RTICError? Error)
{
    public string? ErrorMessage => Error?.Message;
}

internal static class RTICImmutable
{
    public static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReadOnlyCollection<T>(values.ToArray());
    }

    public static IReadOnlyDictionary<TKey, TValue> Copy<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue>? values)
        where TKey : notnull
        => values is null
            ? new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>())
            : new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>(values));

    public static ReadOnlyMemory<byte> Copy(ReadOnlyMemory<byte> value)
        => new(value.ToArray());

    public static ReadOnlyMemory<byte>? Copy(ReadOnlyMemory<byte>? value)
        => value is null ? (ReadOnlyMemory<byte>?)null : Copy(value.Value);
}
