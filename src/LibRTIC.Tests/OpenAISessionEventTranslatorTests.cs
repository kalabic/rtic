using System.ClientModel.Primitives;
using LibRTIC.Conversation;
using LibRTIC.Conversation.OpenAI;
using OpenAI.Realtime;
using Xunit;

namespace LibRTIC.Tests;

#pragma warning disable OPENAI002

public sealed class OpenAISessionEventTranslatorTests
{
    public static TheoryData<Type, string> KnownItemVariants => new()
    {
        { typeof(RTICMessageItem), """{"id":"message_1","type":"message","status":"in_progress","role":"assistant","content":[{"type":"output_text","text":"hello"}]}""" },
        { typeof(RTICFunctionCallItem), """{"id":"call_1","type":"function_call","status":"in_progress","call_id":"fc_1","name":"weather","arguments":"{}"}""" },
        { typeof(RTICFunctionCallOutputItem), """{"id":"output_1","type":"function_call_output","status":"completed","call_id":"fc_1","output":"sunny"}""" },
        { typeof(RTICMcpCallItem), """{"id":"mcp_1","type":"mcp_call","server_label":"server","name":"lookup","arguments":"{}","output":"ok"}""" },
        { typeof(RTICMcpApprovalRequestItem), """{"id":"approval_1","type":"mcp_approval_request","server_label":"server","name":"write","arguments":"{}"}""" },
        { typeof(RTICMcpApprovalResponseItem), """{"id":"approval_response_1","type":"mcp_approval_response","approval_request_id":"approval_1","approve":true,"reason":"allowed"}""" },
        { typeof(RTICMcpListToolsItem), """{"id":"tools_1","type":"mcp_list_tools","server_label":"server","tools":[{"name":"lookup","description":"Lookup","input_schema":{}}]}""" },
    };

    [Theory]
    [MemberData(
        nameof(ConversationUpdateTranslationTests.CurrentServerUpdates),
        MemberType = typeof(ConversationUpdateTranslationTests))]
    public void EveryCurrentSdkUpdateHasAnExactNeutralMapping(Type neutralType, string json)
    {
        OpenAISessionEventTranslator translator = new();

        RTICSessionEvent translated = translator.Translate(ReadUpdate(json));

        Assert.IsType(neutralType, translated);
        Assert.Equal(ExpectedEventId(neutralType), translated.EventId);
        AssertNoProviderObject(translated);
    }

    [Fact]
    public void TranslatorCoverageMatchesEveryPublicConcreteSdkUpdate()
    {
        Type[] sdkTypes = typeof(RealtimeServerUpdate).Assembly.GetTypes()
            .Where(type =>
                type.IsPublic
                && !type.IsAbstract
                && type.BaseType == typeof(RealtimeServerUpdate))
            .OrderBy(type => type.FullName)
            .ToArray();

        Assert.Equal(46, sdkTypes.Length);
        Assert.Equal(
            sdkTypes,
            OpenAISessionEventTranslator.SupportedUpdateTypes
                .OrderBy(type => type.FullName)
                .ToArray());
    }

    [Fact]
    public void AudioBytesArePreservedWithoutTextConversion()
    {
        OpenAISessionEventTranslator translator = new();
        RTICOutputAudioDelta update = Assert.IsType<RTICOutputAudioDelta>(
            translator.Translate(ReadUpdate(
                """{"type":"response.output_audio.delta","event_id":"event_1","response_id":"response_1","item_id":"item_1","output_index":3,"content_index":2,"delta":"AQID"}""")));

        Assert.Equal(new byte[] { 1, 2, 3 }, update.Audio.ToArray());
    }

    [Theory]
    [MemberData(nameof(KnownItemVariants))]
    public void EveryKnownProviderItemUsesAClosedNeutralVariant(
        Type expectedItemType,
        string itemJson)
    {
        string json =
            """{"type":"conversation.item.created","event_id":"event_1","previous_item_id":null,"item":ITEM}"""
            .Replace("ITEM", itemJson, StringComparison.Ordinal);

        RTICItemCreated update = Assert.IsType<RTICItemCreated>(
            new OpenAISessionEventTranslator().Translate(ReadUpdate(json)));

        Assert.IsType(expectedItemType, update.Item);
        Assert.False(string.IsNullOrWhiteSpace(update.ItemId));
    }

    [Fact]
    public void ResponseMapsUsageMetadataItemsAndNullableAudio()
    {
        const string json =
            """
            {
              "type":"response.done",
              "event_id":"event_1",
              "response":{
                "id":"response_1",
                "object":"realtime.response",
                "conversation_id":"timeline_1",
                "status":"completed",
                "status_details":null,
                "output":[{
                  "id":"audio_1",
                  "type":"message",
                  "status":"completed",
                  "role":"assistant",
                  "content":[{"type":"output_audio","transcript":"hello"}]
                }],
                "output_modalities":["audio"],
                "metadata":{"test":"metadata-value"},
                "usage":{
                  "total_tokens":12,
                  "input_tokens":7,
                  "output_tokens":5,
                  "input_token_details":{
                    "text_tokens":3,
                    "audio_tokens":4,
                    "cached_tokens":1
                  },
                  "output_token_details":{"text_tokens":2,"audio_tokens":3}
                }
              }
            }
            """;

        RTICResponseCompleted update = Assert.IsType<RTICResponseCompleted>(
            new OpenAISessionEventTranslator().Translate(ReadUpdate(json)));

        Assert.Equal("timeline_1", update.Response.TimelineId);
        Assert.Equal(12, update.Response.Usage?.TotalTokenCount);
        Assert.Equal(4, update.Response.Usage?.InputAudioTokenCount);
        Assert.Equal(3, update.Response.Usage?.OutputAudioTokenCount);
        Assert.Equal("metadata-value", update.Response.Metadata["test"]);
        RTICMessageItem item =
            Assert.IsType<RTICMessageItem>(Assert.Single(update.Response.OutputItems));
        RTICAudioContentPart part =
            Assert.IsType<RTICAudioContentPart>(Assert.Single(item.Content));
        Assert.False(part.Audio.HasValue);
        Assert.Equal("hello", part.Transcript);
    }

    [Fact]
    public void ErrorsRateLimitsAndStreamingArgumentsPreserveDiagnosticsAndCursors()
    {
        OpenAISessionEventTranslator translator = new();

        RTICErrorReceived error = Assert.IsType<RTICErrorReceived>(
            translator.Translate(ReadUpdate(
                """{"type":"error","event_id":"event_1","error":{"type":"invalid_request_error","code":"bad_audio","message":"Broken audio","param":"audio","event_id":"client_1"}}""")));
        Assert.Equal("bad_audio", error.Error.Code);
        Assert.Equal("audio", error.Error.Parameter);
        Assert.Equal("client_1", error.Error.RelatedEventId);

        RTICRateLimitsUpdated limits = Assert.IsType<RTICRateLimitsUpdated>(
            translator.Translate(ReadUpdate(
                """{"type":"rate_limits.updated","event_id":"event_2","rate_limits":[{"name":"requests","limit":100,"remaining":99,"reset_seconds":1.5}]}""")));
        RTICRateLimit limit = Assert.Single(limits.RateLimits);
        Assert.Equal(99, limit.RemainingCount);
        Assert.Equal(TimeSpan.FromSeconds(1.5), limit.TimeUntilReset);

        RTICFunctionArgumentsDelta arguments =
            Assert.IsType<RTICFunctionArgumentsDelta>(
                translator.Translate(ReadUpdate(
                    """{"type":"response.function_call_arguments.delta","event_id":"event_3","response_id":"response_1","item_id":"item_1","output_index":3,"call_id":"call_1","delta":"{\"city\":"}""")));
        Assert.Equal("response_1", arguments.ResponseId);
        Assert.Equal("item_1", arguments.ItemId);
        Assert.Equal(3, arguments.OutputIndex);
        Assert.Equal("call_1", arguments.CallId);
        Assert.False(arguments.Delta.IsEmpty);
    }

    [Theory]
    [InlineData("mcp_list_tools.in_progress", RTICMcpToolsListStatus.InProgress)]
    [InlineData("mcp_list_tools.completed", RTICMcpToolsListStatus.Completed)]
    [InlineData("mcp_list_tools.failed", RTICMcpToolsListStatus.Failed)]
    public void McpToolListLifecycleIsPreserved(
        string providerType,
        RTICMcpToolsListStatus expected)
    {
        string json =
            """{"type":"TYPE","event_id":"event_1","item_id":"item_1"}"""
            .Replace("\"TYPE\"", $"\"{providerType}\"", StringComparison.Ordinal);

        RTICMcpToolsListed update = Assert.IsType<RTICMcpToolsListed>(
            new OpenAISessionEventTranslator().Translate(ReadUpdate(json)));

        Assert.Equal(expected, update.Status);
    }

    [Theory]
    [InlineData("completed", RTICResponseStatus.Completed)]
    [InlineData("cancelled", RTICResponseStatus.Cancelled)]
    [InlineData("failed", RTICResponseStatus.Failed)]
    [InlineData("incomplete", RTICResponseStatus.Incomplete)]
    [InlineData("future_status", RTICResponseStatus.Unknown)]
    public void ResponseTerminalStateIsMappedExplicitly(
        string providerStatus,
        RTICResponseStatus expected)
    {
        string json =
            """{"type":"response.done","event_id":"event_1","response":{"id":"response_1","object":"realtime.response","conversation_id":"conversation_1","status":"completed","status_details":null,"output":[],"output_modalities":["text"],"metadata":{},"usage":null}}"""
            .Replace(
                "\"status\":\"completed\"",
                $"\"status\":\"{providerStatus}\"",
                StringComparison.Ordinal);

        RTICResponseCompleted update = Assert.IsType<RTICResponseCompleted>(
            new OpenAISessionEventTranslator().Translate(ReadUpdate(json)));

        Assert.Equal(expected, update.Response.Status);
    }

    [Fact]
    public void UnknownProviderEventContainsDiagnosticsButNotTheProviderObject()
    {
        RTICUnknownProviderEvent update = Assert.IsType<RTICUnknownProviderEvent>(
            new OpenAISessionEventTranslator().Translate(ReadUpdate(
                """{"type":"future.server.event","event_id":"future_1","new_field":{"value":42}}""")));

        Assert.Equal("OpenAI", update.ProviderName);
        Assert.Equal(RTICEventId.Unknown, update.EventId);
        AssertNoProviderObject(update);
    }

    private static RealtimeServerUpdate ReadUpdate(string json)
        => ModelReaderWriter.Read<RealtimeServerUpdate>(
            BinaryData.FromString(json),
            ModelReaderWriterOptions.Json)
            ?? throw new InvalidDataException(
                "The SDK did not deserialize the server update.");

    private static void AssertNoProviderObject(RTICSessionEvent update)
    {
        Assert.DoesNotContain(
            update.GetType().GetProperties(),
            property => property.PropertyType.Namespace?.StartsWith(
                "OpenAI",
                StringComparison.Ordinal) == true);
    }

    internal static RTICEventId ExpectedEventId(Type neutralType)
        => neutralType.Name switch
        {
            nameof(RTICSessionCreated) => RTICEventId.SessionCreated,
            nameof(RTICSessionConfigured) => RTICEventId.SessionConfigured,
            nameof(RTICTimelineCreated) => RTICEventId.TimelineCreated,
            nameof(RTICItemAdded) => RTICEventId.ItemAdded,
            nameof(RTICItemCreated) => RTICEventId.ItemCreated,
            nameof(RTICItemCompleted) => RTICEventId.ItemCompleted,
            nameof(RTICItemRetrieved) => RTICEventId.ItemRetrieved,
            nameof(RTICItemDeleted) => RTICEventId.ItemDeleted,
            nameof(RTICItemTruncated) => RTICEventId.ItemTruncated,
            nameof(RTICInputAudioCleared) => RTICEventId.InputAudioCleared,
            nameof(RTICInputAudioCommitted) => RTICEventId.InputAudioCommitted,
            nameof(RTICInputAudioTimedOut) => RTICEventId.InputAudioTimedOut,
            nameof(RTICInputSpeechStarted) => RTICEventId.InputSpeechStarted,
            nameof(RTICInputSpeechFinished) => RTICEventId.InputSpeechFinished,
            nameof(RTICDtmfReceived) => RTICEventId.DtmfReceived,
            nameof(RTICInputTranscriptionDelta) => RTICEventId.InputTranscriptionDelta,
            nameof(RTICInputTranscriptionCompleted) => RTICEventId.InputTranscriptionCompleted,
            nameof(RTICInputTranscriptionFailed) => RTICEventId.InputTranscriptionFailed,
            nameof(RTICInputTranscriptionSegment) => RTICEventId.InputTranscriptionSegment,
            nameof(RTICResponseStarted) => RTICEventId.ResponseStarted,
            nameof(RTICResponseCompleted) => RTICEventId.ResponseCompleted,
            nameof(RTICOutputItemStarted) => RTICEventId.OutputItemStarted,
            nameof(RTICOutputItemCompleted) => RTICEventId.OutputItemCompleted,
            nameof(RTICOutputContentPartStarted) => RTICEventId.OutputContentPartStarted,
            nameof(RTICOutputContentPartCompleted) => RTICEventId.OutputContentPartCompleted,
            nameof(RTICOutputAudioDelta) => RTICEventId.OutputAudioDelta,
            nameof(RTICOutputAudioCompleted) => RTICEventId.OutputAudioCompleted,
            nameof(RTICOutputTextDelta) => RTICEventId.OutputTextDelta,
            nameof(RTICOutputTextCompleted) => RTICEventId.OutputTextCompleted,
            nameof(RTICOutputTranscriptDelta) => RTICEventId.OutputTranscriptDelta,
            nameof(RTICOutputTranscriptCompleted) => RTICEventId.OutputTranscriptCompleted,
            nameof(RTICFunctionArgumentsDelta) => RTICEventId.FunctionArgumentsDelta,
            nameof(RTICFunctionArgumentsCompleted) => RTICEventId.FunctionArgumentsCompleted,
            nameof(RTICMcpToolsListed) => RTICEventId.McpToolsListed,
            nameof(RTICMcpCallStarted) => RTICEventId.McpCallStarted,
            nameof(RTICMcpCallArgumentsDelta) => RTICEventId.McpCallArgumentsDelta,
            nameof(RTICMcpCallArgumentsCompleted) => RTICEventId.McpCallArgumentsCompleted,
            nameof(RTICMcpCallCompleted) => RTICEventId.McpCallCompleted,
            nameof(RTICMcpCallFailed) => RTICEventId.McpCallFailed,
            nameof(RTICErrorReceived) => RTICEventId.ErrorReceived,
            nameof(RTICRateLimitsUpdated) => RTICEventId.RateLimitsUpdated,
            nameof(RTICOutputAudioPlaybackStarted) => RTICEventId.OutputAudioPlaybackStarted,
            nameof(RTICOutputAudioPlaybackCompleted) => RTICEventId.OutputAudioPlaybackCompleted,
            nameof(RTICOutputAudioPlaybackCleared) => RTICEventId.OutputAudioPlaybackCleared,
            nameof(RTICUnknownProviderEvent) => RTICEventId.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(neutralType)),
        };
}

#pragma warning restore OPENAI002
