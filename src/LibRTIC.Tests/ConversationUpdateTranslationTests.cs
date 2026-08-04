using System.ClientModel.Primitives;
using System.Diagnostics.Tracing;
using System.Reflection;
using DotBase.Log;
using LibRTIC.Conversation;
using LibRTIC.Conversation.OpenAI.Realtime;
using LibRTIC.MiniTaskLib;
using OpenAI.Realtime;
using Xunit;

namespace LibRTIC.Tests;

#pragma warning disable OPENAI002

public sealed class ConversationUpdateTranslationTests
{
    public static TheoryData<Type, string> CurrentServerUpdates => new()
    {
        { typeof(RTICTimelineCreated), """{"type":"conversation.created","event_id":"event_1","conversation":{"id":"conversation_1","object":"realtime.conversation"}}""" },
        { typeof(RTICItemAdded), ItemUpdate("conversation.item.added") },
        { typeof(RTICItemCreated), ItemUpdate("conversation.item.created") },
        { typeof(RTICItemDeleted), """{"type":"conversation.item.deleted","event_id":"event_1","item_id":"item_1"}""" },
        { typeof(RTICItemCompleted), ItemUpdate("conversation.item.done") },
        { typeof(RTICInputTranscriptionCompleted), """{"type":"conversation.item.input_audio_transcription.completed","event_id":"event_1","item_id":"item_1","content_index":2,"transcript":"hello","logprobs":[{"token":"hello","bytes":[104,101,108,108,111],"logprob":-0.1}],"usage":{"type":"tokens","total_tokens":3,"input_tokens":2,"output_tokens":1}}""" },
        { typeof(RTICInputTranscriptionDelta), """{"type":"conversation.item.input_audio_transcription.delta","event_id":"event_1","item_id":"item_1","content_index":2,"delta":"hel","logprobs":[{"token":"hel","bytes":[104,101,108],"logprob":-0.2}]}""" },
        { typeof(RTICInputTranscriptionFailed), """{"type":"conversation.item.input_audio_transcription.failed","event_id":"event_1","item_id":"item_1","content_index":2,"error":{"type":"transcription_error","code":"audio_unintelligible","message":"Could not transcribe","param":"audio","event_id":"client_1"}}""" },
        { typeof(RTICInputTranscriptionSegment), """{"type":"conversation.item.input_audio_transcription.segment","event_id":"event_1","item_id":"item_1","content_index":2,"id":"segment_1","start":0.25,"end":0.75,"speaker":"speaker_0","text":"hello"}""" },
        { typeof(RTICItemRetrieved), ItemUpdate("conversation.item.retrieved", false) },
        { typeof(RTICItemTruncated), """{"type":"conversation.item.truncated","event_id":"event_1","item_id":"item_1","content_index":2,"audio_end_ms":1250}""" },
        { typeof(RTICErrorReceived), """{"type":"error","event_id":"event_1","error":{"type":"invalid_request_error","code":"bad_audio","message":"Broken audio","param":"audio","event_id":"client_1"}}""" },
        { typeof(RTICInputAudioCleared), """{"type":"input_audio_buffer.cleared","event_id":"event_1"}""" },
        { typeof(RTICInputAudioCommitted), """{"type":"input_audio_buffer.committed","event_id":"event_1","previous_item_id":"item_0","item_id":"item_1"}""" },
        { typeof(RTICDtmfReceived), """{"type":"input_audio_buffer.dtmf_event_received","event":"5","received_at":1784894400}""" },
        { typeof(RTICInputSpeechStarted), """{"type":"input_audio_buffer.speech_started","event_id":"event_1","audio_start_ms":250,"item_id":"item_1"}""" },
        { typeof(RTICInputSpeechFinished), """{"type":"input_audio_buffer.speech_stopped","event_id":"event_1","audio_end_ms":1250,"item_id":"item_1"}""" },
        { typeof(RTICInputAudioTimedOut), """{"type":"input_audio_buffer.timeout_triggered","event_id":"event_1","audio_start_ms":250,"audio_end_ms":1250,"item_id":"item_1"}""" },
        { typeof(RTICMcpToolsListed), """{"type":"mcp_list_tools.completed","event_id":"event_1","item_id":"item_1"}""" },
        { typeof(RTICOutputAudioPlaybackCleared), """{"type":"output_audio_buffer.cleared","event_id":"event_1","response_id":"response_1"}""" },
        { typeof(RTICOutputAudioPlaybackStarted), """{"type":"output_audio_buffer.started","event_id":"event_1","response_id":"response_1"}""" },
        { typeof(RTICOutputAudioPlaybackCompleted), """{"type":"output_audio_buffer.stopped","event_id":"event_1","response_id":"response_1"}""" },
        { typeof(RTICRateLimitsUpdated), """{"type":"rate_limits.updated","event_id":"event_1","rate_limits":[{"name":"requests","limit":100,"remaining":99,"reset_seconds":1.5},{"name":"tokens","limit":1000,"remaining":900,"reset_seconds":2.5}]}""" },
        { typeof(RTICOutputContentPartStarted), ContentPartUpdate("response.content_part.added") },
        { typeof(RTICOutputContentPartCompleted), ContentPartUpdate("response.content_part.done") },
        { typeof(RTICResponseStarted), ResponseUpdate("response.created", "in_progress") },
        { typeof(RTICResponseCompleted), ResponseUpdate("response.done", "completed") },
        { typeof(RTICFunctionArgumentsDelta), """{"type":"response.function_call_arguments.delta","event_id":"event_1","response_id":"response_1","item_id":"item_1","output_index":3,"call_id":"call_1","delta":"{\"city\":"}""" },
        { typeof(RTICFunctionArgumentsCompleted), """{"type":"response.function_call_arguments.done","event_id":"event_1","response_id":"response_1","item_id":"item_1","output_index":3,"call_id":"call_1","name":"weather","arguments":"{\"city\":\"Zagreb\"}"}""" },
        { typeof(RTICMcpCallArgumentsDelta), """{"type":"response.mcp_call_arguments.delta","event_id":"event_1","response_id":"response_1","item_id":"item_1","output_index":3,"delta":"{\"query\":" ,"obfuscation":"opaque"}""" },
        { typeof(RTICMcpCallArgumentsCompleted), """{"type":"response.mcp_call_arguments.done","event_id":"event_1","response_id":"response_1","item_id":"item_1","output_index":3,"arguments":"{\"query\":\"status\"}"}""" },
        { typeof(RTICMcpCallCompleted), """{"type":"response.mcp_call.completed","event_id":"event_1","item_id":"item_1","output_index":3}""" },
        { typeof(RTICMcpCallFailed), """{"type":"response.mcp_call.failed","event_id":"event_1","item_id":"item_1","output_index":3}""" },
        { typeof(RTICMcpCallStarted), """{"type":"response.mcp_call.in_progress","event_id":"event_1","item_id":"item_1","output_index":3}""" },
        { typeof(RTICOutputAudioDelta), OutputCursorUpdate("response.output_audio.delta", ",\"delta\":\"AQIDBA==\"") },
        { typeof(RTICOutputAudioCompleted), OutputCursorUpdate("response.output_audio.done") },
        { typeof(RTICOutputTranscriptDelta), OutputCursorUpdate("response.output_audio_transcript.delta", ",\"delta\":\"hel\"") },
        { typeof(RTICOutputTranscriptCompleted), OutputCursorUpdate("response.output_audio_transcript.done", ",\"transcript\":\"hello\"") },
        { typeof(RTICOutputItemStarted), OutputItemUpdate("response.output_item.added") },
        { typeof(RTICOutputItemCompleted), OutputItemUpdate("response.output_item.done") },
        { typeof(RTICOutputTextDelta), OutputCursorUpdate("response.output_text.delta", ",\"delta\":\"hel\"") },
        { typeof(RTICOutputTextCompleted), OutputCursorUpdate("response.output_text.done", ",\"text\":\"hello\"") },
        { typeof(RTICSessionCreated), SessionUpdate("session.created") },
        { typeof(RTICSessionConfigured), SessionUpdate("session.updated") },
    };

    [Theory]
    [MemberData(nameof(CurrentServerUpdates))]
    public async Task EveryCurrentSdkUpdateReachesTheNeutralActionQueue(
        Type expectedType,
        string json)
    {
        RTICSessionEvent translated =
            await DispatchAndReceive(ReadUpdate(json), expectedType);

        Assert.IsType(expectedType, translated);
        Assert.Equal(
            OpenAISessionEventTranslatorTests.ExpectedEventId(expectedType),
            translated.EventId);
    }

    [Fact]
    public async Task InterleavedResponsesPreserveReceiveOrderAndCursors()
    {
        using TestDispatcher dispatcher = new();
        List<RTICOutputTextDelta> received = [];
        dispatcher.Events.Connect<RTICOutputTextDelta>(
            false,
            (_, update) => received.Add(update));

        TaskWithEvents actionQueueTask = dispatcher.RunAsync();
        dispatcher.Dispatch(ReadUpdate(TextDelta("response_1", "item_1", 0, 0, "one")));
        dispatcher.Dispatch(ReadUpdate(TextDelta("response_2", "item_2", 1, 2, "two")));
        dispatcher.Dispatch(ReadUpdate(TextDelta("response_1", "item_3", 3, 4, "three")));
        dispatcher.CompleteAdding();
        await actionQueueTask;

        Assert.Collection(
            received,
            update => AssertCursor(update, "response_1", "item_1", 0, 0, "one"),
            update => AssertCursor(update, "response_2", "item_2", 1, 2, "two"),
            update => AssertCursor(update, "response_1", "item_3", 3, 4, "three"));
    }

    [Fact]
    public async Task TranslationFailureDoesNotStopLaterEvents()
    {
        using TestDispatcher dispatcher = new();
        List<string> received = [];
        dispatcher.Events.Connect<RTICErrorReceived>(
            false,
            (_, update) => received.Add(update.Error.Code ?? "error"));
        dispatcher.Events.Connect<RTICOutputTextDelta>(
            false,
            (_, update) => received.Add(update.Delta));

        TaskWithEvents actionQueueTask = dispatcher.RunAsync();
        dispatcher.Dispatch(ReadUpdate(
            """{"type":"conversation.item.created","event_id":"bad_1","previous_item_id":null,"item":{"type":"message","role":"assistant","content":[]}}"""));
        dispatcher.Dispatch(ReadUpdate(TextDelta(
            "response_1", "item_1", 0, 0, "still-running")));
        dispatcher.CompleteAdding();
        await actionQueueTask;

        Assert.Equal(["provider_translation_failed", "still-running"], received);
    }

    [Fact]
    public async Task ActionQueueCompletionDoesNotLoseQueuedEvents()
    {
        using TestDispatcher dispatcher = new();
        int received = 0;
        dispatcher.Events.Connect<RTICOutputTextDelta>(
            false,
            (_, _) => received++);

        TaskWithEvents actionQueueTask = dispatcher.RunAsync();
        for (int i = 0; i < 50; i++)
        {
            dispatcher.Dispatch(ReadUpdate(TextDelta(
                "response_1", $"item_{i}", i, 0, i.ToString())));
        }

        dispatcher.CompleteAdding();
        await actionQueueTask;

        Assert.Equal(50, received);
    }

    private static readonly MethodInfo ConnectMethod =
        typeof(ConversationUpdateTranslationTests).GetMethod(
            nameof(Connect),
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Test connector was not found.");

    private static void Connect<TUpdate>(
        TestDispatcher dispatcher,
        Action<RTICSessionEvent> capture)
        where TUpdate : RTICSessionEvent
        => dispatcher.Events.Connect<TUpdate>(false, (_, update) => capture(update));

    private static async Task<RTICSessionEvent> DispatchAndReceive(
        RealtimeServerUpdate providerUpdate,
        Type expectedType)
    {
        using TestDispatcher dispatcher = new();
        RTICSessionEvent? received = null;
        ConnectMethod.MakeGenericMethod(expectedType).Invoke(
            null,
            [dispatcher, new Action<RTICSessionEvent>(update => received = update)]);

        TaskWithEvents actionQueueTask = dispatcher.RunAsync();
        dispatcher.Dispatch(providerUpdate);
        dispatcher.CompleteAdding();
        await actionQueueTask;

        return received ?? throw new InvalidOperationException(
            $"No {expectedType.Name} update reached the action queue.");
    }

    private static RealtimeServerUpdate ReadUpdate(string json)
        => ModelReaderWriter.Read<RealtimeServerUpdate>(
            BinaryData.FromString(json),
            ModelReaderWriterOptions.Json)
            ?? throw new InvalidDataException(
                "The SDK did not deserialize the server update.");

    private static string ItemUpdate(string type, bool includePreviousItemId = true)
        => $$$"""{"type":"{{{type}}}","event_id":"event_1",{{{(includePreviousItemId ? "\"previous_item_id\":\"item_0\"," : "")}}}"item":{"id":"item_1","type":"message","status":"in_progress","role":"assistant","content":[{"type":"output_text","text":"hello"}]}}""";

    private static string ContentPartUpdate(string type)
        => $$$"""{"type":"{{{type}}}","event_id":"event_1","response_id":"response_1","item_id":"item_1","output_index":3,"content_index":2,"part":{"type":"audio","transcript":"hello"}}""";

    private static string OutputCursorUpdate(string type, string suffix = "")
        => $$$"""{"type":"{{{type}}}","event_id":"event_1","response_id":"response_1","item_id":"item_1","output_index":3,"content_index":2{{{suffix}}}}""";

    private static string OutputItemUpdate(string type)
        => $$$"""{"type":"{{{type}}}","event_id":"event_1","response_id":"response_1","output_index":3,"item":{"id":"item_1","type":"message","status":"in_progress","role":"assistant","content":[]}}""";

    private static string TextDelta(
        string responseId,
        string itemId,
        int outputIndex,
        int contentIndex,
        string delta)
        => $$$"""{"type":"response.output_text.delta","event_id":"event_1","response_id":"{{{responseId}}}","item_id":"{{{itemId}}}","output_index":{{{outputIndex}}},"content_index":{{{contentIndex}}},"delta":"{{{delta}}}"}""";

    private static string ResponseUpdate(string type, string status)
        => """{"type":"TYPE","event_id":"event_1","response":{"id":"response_1","object":"realtime.response","conversation_id":"conversation_1","status":"STATUS","status_details":null,"output":[],"output_modalities":["audio","text"],"metadata":{"test":"metadata-value"},"usage":{"total_tokens":12,"input_tokens":7,"output_tokens":5,"input_token_details":{"text_tokens":3,"audio_tokens":4,"cached_tokens":1},"output_token_details":{"text_tokens":2,"audio_tokens":3}}}}"""
            .Replace("\"TYPE\"", $"\"{type}\"", StringComparison.Ordinal)
            .Replace("\"STATUS\"", $"\"{status}\"", StringComparison.Ordinal);

    private static string SessionUpdate(string type)
        => """{"type":"TYPE","event_id":"event_1","session":{"id":"session_1","type":"realtime","model":"gpt-realtime","output_modalities":["audio"],"instructions":"Be helpful.","audio":{"input":{"format":{"type":"audio/pcm","rate":24000},"transcription":{"model":"gpt-4o-mini-transcribe"},"turn_detection":{"type":"server_vad"}},"output":{"format":{"type":"audio/pcm"},"voice":"alloy"}},"tools":[],"tool_choice":"auto","max_output_tokens":1024}}"""
            .Replace("\"TYPE\"", $"\"{type}\"", StringComparison.Ordinal);

    private static void AssertCursor(
        RTICOutputTextDelta update,
        string responseId,
        string itemId,
        int outputIndex,
        int contentIndex,
        string delta)
    {
        Assert.Equal(responseId, update.ResponseId);
        Assert.Equal(itemId, update.ItemId);
        Assert.Equal(outputIndex, update.OutputIndex);
        Assert.Equal(contentIndex, update.ContentIndex);
        Assert.Equal(delta, update.Delta);
    }

    private sealed class TestDispatcher : ConversationUpdatesDispatcher
    {
        public TestDispatcher()
            : base(
                new ConsoleInfo(EventLevel.Critical),
                CancellationToken.None) { }

        public void Dispatch(RealtimeServerUpdate update) => DispatchUpdate(update);
    }
}

#pragma warning restore OPENAI002
