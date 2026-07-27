namespace LibRTIC.Conversation;

/// <summary>
/// Sends provider-neutral commands to a running Realtime conversation.
/// </summary>
public sealed class RTICConversationControl
{
    private readonly Func<RTICResponseRequest, CancellationToken, Task> _requestResponse;
    private readonly Func<RTICOutputInterruption, CancellationToken, Task> _interruptOutput;

    internal RTICConversationControl(
        Func<RTICResponseRequest, CancellationToken, Task> requestResponse,
        Func<RTICOutputInterruption, CancellationToken, Task> interruptOutput)
    {
        _requestResponse = requestResponse;
        _interruptOutput = interruptOutput;
    }

    /// <summary>
    /// Requests a response and completes after the command has been sent.
    /// Provider acceptance and response progress are reported through conversation events.
    /// </summary>
    public Task RequestResponseAsync(
        RTICResponseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _requestResponse(request, cancellationToken);
    }

    /// <summary>
    /// Stops correlated output when requested and truncates it to what was actually played.
    /// The operation is serialized with other conversation commands, but providers may still
    /// represent it as multiple non-transactional protocol commands.
    /// </summary>
    public Task InterruptOutputAsync(
        RTICOutputInterruption request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _interruptOutput(request, cancellationToken);
    }
}

/// <summary>
/// Options for one response request. Blank instructions use provider session defaults.
/// </summary>
public sealed record RTICResponseRequest
{
    public string? Instructions { get; }

    public RTICResponseRequest(string? instructions = null)
    {
        Instructions = instructions;
    }
}

/// <summary>
/// Identifies one output content part without exposing provider SDK types.
/// </summary>
public sealed record RTICOutputCursor
{
    public string ResponseId { get; }

    public string ItemId { get; }

    public int OutputIndex { get; }

    public int ContentIndex { get; }

    public RTICOutputCursor(
        string responseId,
        string itemId,
        int outputIndex,
        int contentIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        ArgumentOutOfRangeException.ThrowIfNegative(outputIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(contentIndex);

        ResponseId = responseId;
        ItemId = itemId;
        OutputIndex = outputIndex;
        ContentIndex = contentIndex;
    }
}

/// <summary>
/// Describes the playback boundary for one correlated output interruption.
/// </summary>
public sealed record RTICOutputInterruption
{
    public RTICOutputCursor Cursor { get; }

    public TimeSpan PlayedThrough { get; }

    public bool CancelResponseIfActive { get; }

    public RTICOutputInterruption(
        RTICOutputCursor cursor,
        TimeSpan playedThrough,
        bool cancelResponseIfActive)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        if (playedThrough < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(playedThrough));
        }

        Cursor = cursor;
        PlayedThrough = playedThrough;
        CancelResponseIfActive = cancelResponseIfActive;
    }
}

public enum RTICConversationControlOperation
{
    RequestResponse = 0,
    InterruptOutput = 1,
}

/// <summary>
/// Reports a local transport failure while sending a conversation control operation.
/// Later provider-side rejections are delivered as neutral conversation error events.
/// </summary>
public sealed class RTICConversationControlException : Exception
{
    public RTICConversationControlOperation Operation { get; }

    internal RTICConversationControlException(
        RTICConversationControlOperation operation,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Operation = operation;
    }
}
