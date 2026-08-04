using System.ClientModel.Primitives;
using System.Text.Json;
using AudioFormatLib;
using LibRTIC.Realtime;
using OpenAI.Realtime;

namespace LibRTIC.Conversation.OpenAI;

#pragma warning disable OPENAI002

internal sealed class OpenAISessionEventTranslator
{
    internal static IReadOnlySet<Type> SupportedUpdateTypes { get; } = new HashSet<Type>
    {
        typeof(RealtimeServerUpdateConversationCreated),
        typeof(RealtimeServerUpdateConversationItemAdded),
        typeof(RealtimeServerUpdateConversationItemCreated),
        typeof(RealtimeServerUpdateConversationItemDeleted),
        typeof(RealtimeServerUpdateConversationItemDone),
        typeof(RealtimeServerUpdateConversationItemInputAudioTranscriptionCompleted),
        typeof(RealtimeServerUpdateConversationItemInputAudioTranscriptionDelta),
        typeof(RealtimeServerUpdateConversationItemInputAudioTranscriptionFailed),
        typeof(RealtimeServerUpdateConversationItemInputAudioTranscriptionSegment),
        typeof(RealtimeServerUpdateConversationItemRetrieved),
        typeof(RealtimeServerUpdateConversationItemTruncated),
        typeof(RealtimeServerUpdateError),
        typeof(RealtimeServerUpdateInputAudioBufferCleared),
        typeof(RealtimeServerUpdateInputAudioBufferCommitted),
        typeof(RealtimeServerUpdateInputAudioBufferDtmfEventReceived),
        typeof(RealtimeServerUpdateInputAudioBufferSpeechStarted),
        typeof(RealtimeServerUpdateInputAudioBufferSpeechStopped),
        typeof(RealtimeServerUpdateInputAudioBufferTimeoutTriggered),
        typeof(RealtimeServerUpdateMcpListToolsCompleted),
        typeof(RealtimeServerUpdateMcpListToolsFailed),
        typeof(RealtimeServerUpdateMcpListToolsInProgress),
        typeof(RealtimeServerUpdateOutputAudioBufferCleared),
        typeof(RealtimeServerUpdateOutputAudioBufferStarted),
        typeof(RealtimeServerUpdateOutputAudioBufferStopped),
        typeof(RealtimeServerUpdateRateLimitsUpdated),
        typeof(RealtimeServerUpdateResponseContentPartAdded),
        typeof(RealtimeServerUpdateResponseContentPartDone),
        typeof(RealtimeServerUpdateResponseCreated),
        typeof(RealtimeServerUpdateResponseDone),
        typeof(RealtimeServerUpdateResponseFunctionCallArgumentsDelta),
        typeof(RealtimeServerUpdateResponseFunctionCallArgumentsDone),
        typeof(RealtimeServerUpdateResponseMcpCallArgumentsDelta),
        typeof(RealtimeServerUpdateResponseMcpCallArgumentsDone),
        typeof(RealtimeServerUpdateResponseMcpCallCompleted),
        typeof(RealtimeServerUpdateResponseMcpCallFailed),
        typeof(RealtimeServerUpdateResponseMcpCallInProgress),
        typeof(RealtimeServerUpdateResponseOutputAudioDelta),
        typeof(RealtimeServerUpdateResponseOutputAudioDone),
        typeof(RealtimeServerUpdateResponseOutputAudioTranscriptDelta),
        typeof(RealtimeServerUpdateResponseOutputAudioTranscriptDone),
        typeof(RealtimeServerUpdateResponseOutputItemAdded),
        typeof(RealtimeServerUpdateResponseOutputItemDone),
        typeof(RealtimeServerUpdateResponseOutputTextDelta),
        typeof(RealtimeServerUpdateResponseOutputTextDone),
        typeof(RealtimeServerUpdateSessionCreated),
        typeof(RealtimeServerUpdateSessionUpdated),
    };

    internal RTICSessionEvent Translate(RealtimeServerUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        try
        {
            return TranslateCore(update);
        }
        catch (Exception ex)
        {
            string providerKind = GetProviderKind(update);
            string? eventId = GetEventId(update);
            return new RTICErrorReceived(
                new RTICError(
                    "provider_translation_failed",
                    $"Failed to translate OpenAI event '{providerKind}': {ex.Message}",
                    null,
                    eventId,
                    "translation_error"));
        }
    }

    private static RTICSessionEvent TranslateCore(RealtimeServerUpdate update)
        => update switch
        {
            RealtimeServerUpdateConversationCreated value => new RTICTimelineCreated(
                value.Conversation.Id),
            RealtimeServerUpdateConversationItemAdded value => new RTICItemAdded(
                TranslateItem(value.Item), value.PreviousItemId),
            RealtimeServerUpdateConversationItemCreated value => new RTICItemCreated(
                TranslateItem(value.Item), value.PreviousItemId),
            RealtimeServerUpdateConversationItemDeleted value => new RTICItemDeleted(
                value.ItemId),
            RealtimeServerUpdateConversationItemDone value => new RTICItemCompleted(
                TranslateItem(value.Item), value.PreviousItemId),
            RealtimeServerUpdateConversationItemInputAudioTranscriptionCompleted value =>
                new RTICInputTranscriptionCompleted(
                    value.ItemId,
                    value.ContentIndex,
                    value.Transcript,
                    TranslateTranscriptionUsage(value.Usage),
                    TranslateLogProbabilities(value.Logprobs)),
            RealtimeServerUpdateConversationItemInputAudioTranscriptionDelta value =>
                new RTICInputTranscriptionDelta(
                    value.ItemId,
                    value.ContentIndex,
                    value.Delta,
                    TranslateLogProbabilities(value.Logprobs)),
            RealtimeServerUpdateConversationItemInputAudioTranscriptionFailed value =>
                new RTICInputTranscriptionFailed(
                    value.ItemId,
                    value.ContentIndex,
                    TranslateError(value.Error)),
            RealtimeServerUpdateConversationItemInputAudioTranscriptionSegment value =>
                new RTICInputTranscriptionSegment(
                    value.ItemId,
                    value.ContentIndex,
                    value.Id,
                    value.Start,
                    value.End,
                    value.Speaker,
                    value.Text),
            RealtimeServerUpdateConversationItemRetrieved value => new RTICItemRetrieved(
                TranslateItem(value.Item)),
            RealtimeServerUpdateConversationItemTruncated value => new RTICItemTruncated(
                value.ItemId,
                value.ContentIndex,
                value.AudioEndTime),
            RealtimeServerUpdateError value => new RTICErrorReceived(
                TranslateError(value.Error)),
            RealtimeServerUpdateInputAudioBufferCleared => new RTICInputAudioCleared(),
            RealtimeServerUpdateInputAudioBufferCommitted value => new RTICInputAudioCommitted(
                value.ItemId, value.PreviousItemId),
            RealtimeServerUpdateInputAudioBufferDtmfEventReceived value => new RTICDtmfReceived(
                value.Event, value.ReceivedAt),
            RealtimeServerUpdateInputAudioBufferSpeechStarted value => new RTICInputSpeechStarted(
                value.ItemId, value.AudioStartTime),
            RealtimeServerUpdateInputAudioBufferSpeechStopped value => new RTICInputSpeechFinished(
                value.ItemId, value.AudioEndTime),
            RealtimeServerUpdateInputAudioBufferTimeoutTriggered value => new RTICInputAudioTimedOut(
                value.ItemId,
                value.AudioStartTime,
                value.AudioEndTime),
            RealtimeServerUpdateMcpListToolsCompleted value => new RTICMcpToolsListed(
                value.ItemId, RTICMcpToolsListStatus.Completed),
            RealtimeServerUpdateMcpListToolsFailed value => new RTICMcpToolsListed(
                value.ItemId, RTICMcpToolsListStatus.Failed),
            RealtimeServerUpdateMcpListToolsInProgress value => new RTICMcpToolsListed(
                value.ItemId, RTICMcpToolsListStatus.InProgress),
            RealtimeServerUpdateOutputAudioBufferCleared value =>
                new RTICOutputAudioPlaybackCleared(
                    value.ResponseId),
            RealtimeServerUpdateOutputAudioBufferStarted value =>
                new RTICOutputAudioPlaybackStarted(
                    value.ResponseId),
            RealtimeServerUpdateOutputAudioBufferStopped value =>
                new RTICOutputAudioPlaybackCompleted(
                    value.ResponseId),
            RealtimeServerUpdateRateLimitsUpdated value => new RTICRateLimitsUpdated(
                TranslateRateLimits(value.RateLimitDetails)),
            RealtimeServerUpdateResponseContentPartAdded value =>
                new RTICOutputContentPartStarted(
                    value.ResponseId,
                    value.ItemId,
                    value.OutputIndex,
                    value.ContentIndex,
                    TranslateContentPart(value.Part)),
            RealtimeServerUpdateResponseContentPartDone value =>
                new RTICOutputContentPartCompleted(
                    value.ResponseId,
                    value.ItemId,
                    value.OutputIndex,
                    value.ContentIndex,
                    TranslateContentPart(value.Part)),
            RealtimeServerUpdateResponseCreated value => new RTICResponseStarted(
                TranslateResponse(value.Response)),
            RealtimeServerUpdateResponseDone value => new RTICResponseCompleted(
                TranslateResponse(value.Response)),
            RealtimeServerUpdateResponseFunctionCallArgumentsDelta value =>
                new RTICFunctionArgumentsDelta(
                    value.ResponseId,
                    value.ItemId,
                    value.OutputIndex,
                    value.CallId,
                    value.Delta.ToMemory()),
            RealtimeServerUpdateResponseFunctionCallArgumentsDone value =>
                new RTICFunctionArgumentsCompleted(
                    value.ResponseId,
                    value.ItemId,
                    value.OutputIndex,
                    value.CallId,
                    value.FunctionName,
                    value.FunctionArguments.ToMemory()),
            RealtimeServerUpdateResponseMcpCallArgumentsDelta value =>
                new RTICMcpCallArgumentsDelta(
                    value.ResponseId,
                    value.ItemId,
                    value.OutputIndex,
                    value.Delta.ToMemory(),
                    value.Obfuscation),
            RealtimeServerUpdateResponseMcpCallArgumentsDone value =>
                new RTICMcpCallArgumentsCompleted(
                    value.ResponseId,
                    value.ItemId,
                    value.OutputIndex,
                    value.ToolArguments.ToMemory()),
            RealtimeServerUpdateResponseMcpCallCompleted value => new RTICMcpCallCompleted(
                value.ItemId, value.OutputIndex),
            RealtimeServerUpdateResponseMcpCallFailed value => new RTICMcpCallFailed(
                value.ItemId, value.OutputIndex),
            RealtimeServerUpdateResponseMcpCallInProgress value => new RTICMcpCallStarted(
                value.ItemId, value.OutputIndex),
            RealtimeServerUpdateResponseOutputAudioDelta value => new RTICOutputAudioDelta(
                value.ResponseId,
                value.ItemId,
                value.OutputIndex,
                value.ContentIndex,
                RealtimeAudioContract.CreatePacket(value.Delta.ToMemory().Span)),
            RealtimeServerUpdateResponseOutputAudioDone value => new RTICOutputAudioCompleted(
                value.ResponseId,
                value.ItemId,
                value.OutputIndex,
                value.ContentIndex),
            RealtimeServerUpdateResponseOutputAudioTranscriptDelta value =>
                new RTICOutputTranscriptDelta(
                    value.ResponseId,
                    value.ItemId,
                    value.OutputIndex,
                    value.ContentIndex,
                    value.Delta),
            RealtimeServerUpdateResponseOutputAudioTranscriptDone value =>
                new RTICOutputTranscriptCompleted(
                    value.ResponseId,
                    value.ItemId,
                    value.OutputIndex,
                    value.ContentIndex,
                    value.Transcript),
            RealtimeServerUpdateResponseOutputItemAdded value => new RTICOutputItemStarted(
                value.ResponseId,
                TranslateItem(value.Item),
                value.OutputIndex),
            RealtimeServerUpdateResponseOutputItemDone value => new RTICOutputItemCompleted(
                value.ResponseId,
                TranslateItem(value.Item),
                value.OutputIndex),
            RealtimeServerUpdateResponseOutputTextDelta value => new RTICOutputTextDelta(
                value.ResponseId,
                value.ItemId,
                value.OutputIndex,
                value.ContentIndex,
                value.Delta),
            RealtimeServerUpdateResponseOutputTextDone value => new RTICOutputTextCompleted(
                value.ResponseId,
                value.ItemId,
                value.OutputIndex,
                value.ContentIndex,
                value.Text),
            RealtimeServerUpdateSessionCreated value => new RTICSessionCreated(
                TranslateSession(value.Session)),
            RealtimeServerUpdateSessionUpdated value => new RTICSessionConfigured(
                TranslateSession(value.Session)),
            _ => new RTICUnknownProviderEvent(
                "OpenAI",
                GetProviderKind(update)),
        };

    private static RTICItem TranslateItem(RealtimeItem item)
        => item switch
        {
            RealtimeMessageItem value => new RTICMessageItem(
                value.Id,
                value.Status?.ToString(),
                value.Role.ToString(),
                value.Content.Select(TranslateMessageContentPart)),
            RealtimeFunctionCallItem value => new RTICFunctionCallItem(
                value.Id,
                value.Status?.ToString(),
                value.FunctionName,
                value.CallId,
                ToMemory(value.FunctionArguments)),
            RealtimeFunctionCallOutputItem value => new RTICFunctionCallOutputItem(
                value.Id,
                value.Status?.ToString(),
                value.CallId,
                value.FunctionOutput),
            RealtimeMcpToolCallItem value => new RTICMcpCallItem(
                value.Id,
                value.ServerLabel,
                value.ToolName,
                ToMemory(value.ToolArguments),
                value.ToolOutput,
                value.Error is null ? null : TranslateError(value.Error),
                value.ApprovalRequestId),
            RealtimeMcpToolCallApprovalRequestItem value => new RTICMcpApprovalRequestItem(
                value.Id,
                value.ServerLabel,
                value.ToolName,
                ToMemory(value.ToolArguments)),
            RealtimeMcpToolCallApprovalResponseItem value => new RTICMcpApprovalResponseItem(
                value.Id,
                value.ApprovalRequestId,
                value.Approved,
                value.Reason),
            RealtimeMcpToolDefinitionListItem value => new RTICMcpListToolsItem(
                value.Id,
                value.ServerLabel,
                value.ToolDefinitions.Select(TranslateMcpTool)),
            _ => new RTICUnknownItem(
                GetRequiredItemId(item),
                GetProperty(item, "Status"),
                GetProviderKind(item)),
        };

    private static RTICContentPart TranslateMessageContentPart(
        RealtimeMessageContentPart part)
        => part switch
        {
            RealtimeInputAudioMessageContentPart value => new RTICAudioContentPart(
                TranslateOptionalAudio(value.AudioBytes),
                value.Transcript,
                true),
            RealtimeOutputAudioMessageContentPart value => new RTICAudioContentPart(
                TranslateOptionalAudio(value.AudioBytes),
                value.Transcript,
                false),
            RealtimeInputTextMessageContentPart value => new RTICTextContentPart(
                value.Text, true),
            RealtimeOutputTextMessageContentPart value => new RTICTextContentPart(
                value.Text, false),
            _ => new RTICUnknownContentPart(GetProviderKind(part)),
        };

    private static RTICContentPart TranslateContentPart(RealtimeResponseContentPart part)
    {
        if (part.Kind == RealtimeResponseContentPartKind.Text)
        {
            return new RTICTextContentPart(part.Text, false);
        }

        if (part.Kind == RealtimeResponseContentPartKind.Audio)
        {
            return new RTICAudioContentPart(
                TranslateOptionalAudio(part.Audio),
                part.Transcript,
                false);
        }

        return new RTICUnknownContentPart(
            part.Kind?.ToString() ?? "unknown",
            part.Text,
            part.Transcript,
            part.Audio is not null);
    }

    private static RTICResponse TranslateResponse(RealtimeResponse response)
        => new(
            response.Id,
            TranslateResponseStatus(response.Status),
            response.OutputItems.Select(TranslateItem),
            response.OutputModalities.Select(static value => value.ToString()),
            response.Metadata.ToDictionary(
                static pair => pair.Key,
                static pair => TranslateMetadataValue(pair.Value)),
            TranslateUsage(response.Usage),
            TranslateStatusDetails(response.StatusDetails),
            response.ConversationId);

    private static RTICUsage? TranslateUsage(RealtimeResponseUsage? usage)
        => usage is null
            ? null
            : new RTICUsage(
                usage.TotalTokenCount,
                usage.InputTokenCount,
                usage.OutputTokenCount,
                usage.InputTokenDetails?.TextTokenCount,
                usage.InputTokenDetails?.AudioTokenCount,
                usage.InputTokenDetails?.ImageTokenCount,
                usage.InputTokenDetails?.CachedTokenCount,
                usage.OutputTokenDetails?.TextTokenCount,
                usage.OutputTokenDetails?.AudioTokenCount);

    private static RTICTranscriptionUsage? TranslateTranscriptionUsage(
        RealtimeTranscriptionUsage? usage)
        => usage switch
        {
            null => null,
            RealtimeTranscriptionTokenUsage value => new RTICTranscriptionUsage(
                RTICTranscriptionUsageKind.Tokens,
                value.TotalTokenCount,
                value.InputTokenCount,
                value.OutputTokenCount),
            RealtimeTranscriptionDurationUsage value => new RTICTranscriptionUsage(
                RTICTranscriptionUsageKind.Duration,
                Duration: value.Duration),
            _ => new RTICTranscriptionUsage(RTICTranscriptionUsageKind.Unknown),
        };

    private static RTICError TranslateError(RealtimeError error)
        => new(
            error.Code,
            error.Message,
            error.ParameterName,
            error.EventId,
            error.Kind);

    private static IEnumerable<RTICRateLimit> TranslateRateLimits(
        IEnumerable<RealtimeRateLimitDetails> limits)
        => limits.Select(static value => new RTICRateLimit(
            value.Name?.ToString(),
            value.Limit,
            value.RemainingCount,
            value.TimeUntilReset));

    private static IEnumerable<RTICLogProbability> TranslateLogProbabilities(
        IEnumerable<RealtimeLogProbabilityDetails> values)
        => values.Select(static value => new RTICLogProbability(
            value.Token,
            value.LogProbability,
            value.Utf8Bytes));

    private static RTICResponseStatusDetails? TranslateStatusDetails(
        RealtimeResponseStatusDetails? details)
        => details is null
            ? null
            : new RTICResponseStatusDetails(
                details.Kind?.ToString(),
                details.Reason?.ToString(),
                details.Error is null ? null : TranslateError(details.Error));

    private static RTICResponseStatus TranslateResponseStatus(
        RealtimeResponseStatus? status)
    {
        if (status == RealtimeResponseStatus.InProgress)
        {
            return RTICResponseStatus.InProgress;
        }

        if (status == RealtimeResponseStatus.Completed)
        {
            return RTICResponseStatus.Completed;
        }

        if (status == RealtimeResponseStatus.Cancelled)
        {
            return RTICResponseStatus.Cancelled;
        }

        if (status == RealtimeResponseStatus.Failed)
        {
            return RTICResponseStatus.Failed;
        }

        if (status == RealtimeResponseStatus.Incomplete)
        {
            return RTICResponseStatus.Incomplete;
        }

        return RTICResponseStatus.Unknown;
    }

    private static RTICSessionInfo TranslateSession(RealtimeSession session)
        => session is RealtimeConversationSession conversation
            ? new RTICSessionInfo(
                conversation.Model,
                conversation.Instructions,
                conversation.OutputModalities.Select(static value => value.ToString()),
                conversation.MaxOutputTokenCount?.ToString())
            : new RTICSessionInfo(null, null, [], null);

    private static RTICMcpTool TranslateMcpTool(RealtimeMcpToolDefinition tool)
        => new(
            tool.Name,
            tool.Description,
            tool.InputSchema?.ToString(),
            tool.Annotations?.ToString());

    private static ReadOnlyMemory<byte>? ToMemory(BinaryData? value)
        => value?.ToMemory();

    private static ReadOnlyMemory<byte>? TranslateOptionalBinary(BinaryData? value)
    {
        if (value is null)
        {
            return null;
        }

        byte[] bytes = value.ToArray();
        return bytes.Length == 0
            ? (ReadOnlyMemory<byte>?)null
            : new ReadOnlyMemory<byte>(bytes);
    }

    private static AudioPacket? TranslateOptionalAudio(BinaryData? value)
    {
        if (value is null)
        {
            return null;
        }

        ReadOnlyMemory<byte> bytes = value.ToMemory();
        return bytes.Length == 0
            ? null
            : RealtimeAudioContract.CreatePacket(bytes.Span);
    }

    private static string? TranslateMetadataValue(BinaryData? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            using JsonDocument json = JsonDocument.Parse(value.ToMemory());
            return json.RootElement.ValueKind == JsonValueKind.String
                ? json.RootElement.GetString()
                : json.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return value.ToString();
        }
    }

    private static string GetRequiredItemId(RealtimeItem item)
        => GetProperty(item, "Id")
            ?? throw new InvalidDataException(
                $"OpenAI item '{item.GetType().FullName}' did not expose an identifier.");

    private static string? GetEventId(object update)
        => GetProperty(update, "EventId")
            ?? GetWireProperty(update, "event_id");

    private static string GetProviderKind(object value)
        => GetProperty(value, "Kind")
            ?? GetProperty(value, "Type")
            ?? GetWireProperty(value, "type")
            ?? value.GetType().FullName
            ?? value.GetType().Name;

    private static string? GetProperty(object value, string propertyName)
        => value.GetType().GetProperty(propertyName)?.GetValue(value)?.ToString();

    private static string? GetWireProperty(object value, string propertyName)
    {
        try
        {
            BinaryData data = ModelReaderWriter.Write(
                value,
                ModelReaderWriterOptions.Json);
            using JsonDocument json = JsonDocument.Parse(data);
            return json.RootElement.TryGetProperty(propertyName, out JsonElement property)
                ? property.ToString()
                : null;
        }
        catch
        {
            return null;
        }
    }

}

#pragma warning restore OPENAI002
