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
        { typeof(RTICFunctionCallItem), """{"id":"call_1","type":"function_call","status":"in_progress","call_id":"fc_1","name":"weather","arguments":"{}"}""" },
        { typeof(RTICFunctionCallOutputItem), """{"id":"output_1","type":"function_call_output","status":"completed","call_id":"fc_1","output":"sunny"}""" },
        { typeof(RTICMcpCallItem), """{"id":"mcp_1","type":"mcp_call","server_label":"server","name":"lookup","arguments":"{}","output":"ok"}""" },
        { typeof(RTICMcpApprovalRequestItem), """{"id":"approval_1","type":"mcp_approval_request","server_label":"server","name":"write","arguments":"{}"}""" },
        { typeof(RTICMcpApprovalResponseItem), """{"id":"approval_response_1","type":"mcp_approval_response","approval_request_id":"approval_1","approve":true,"reason":"allowed"}""" },
        { typeof(RTICMcpListToolsItem), """{"id":"tools_1","type":"mcp_list_tools","server_label":"server","tools":[{"name":"lookup","description":"Lookup","input_schema":{}}]}""" },
    };

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
                """{"type":"response.output_audio.delta","event_id":"event_1","response_id":"response_1","item_id":"item_1","output_index":3,"content_index":2,"delta":"AQIDBA=="}""")));

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, update.Audio.ToByteArray());
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

    private static RealtimeServerUpdate ReadUpdate(string json)
        => ModelReaderWriter.Read<RealtimeServerUpdate>(
            BinaryData.FromString(json),
            ModelReaderWriterOptions.Json)
            ?? throw new InvalidDataException(
                "The SDK did not deserialize the server update.");

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
