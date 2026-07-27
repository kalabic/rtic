using LibRTIC.Conversation;
using LibRTIC.Conversation.OpenAI.Realtime;
using OpenAI.Realtime;
using Xunit;

namespace LibRTIC.Tests;

#pragma warning disable OPENAI002

public sealed class OpenAIRealtimeCommandFactoryTests
{
    [Fact]
    public void ResponseRequestMapsBlankInstructionsToProviderDefaults()
    {
        RealtimeClientCommandResponseCreate command =
            OpenAIRealtimeCommandFactory.CreateResponse(
                new RTICResponseRequest("  "),
                "event-1");

        Assert.Equal("event-1", command.EventId);
        Assert.Null(command.ResponseOptions);
    }

    [Fact]
    public void ResponseRequestMapsExplicitInstructions()
    {
        RealtimeClientCommandResponseCreate command =
            OpenAIRealtimeCommandFactory.CreateResponse(
                new RTICResponseRequest("greet briefly"),
                "event-2");

        Assert.Equal("greet briefly", command.ResponseOptions.Instructions);
    }

    [Fact]
    public void CancelCarriesCorrelatedResponseId()
    {
        RealtimeClientCommandResponseCancel command =
            OpenAIRealtimeCommandFactory.CreateCancel(
                "response-7",
                "event-3");

        Assert.Equal("event-3", command.EventId);
        Assert.Equal("response-7", command.ResponseId);
    }

}

#pragma warning restore OPENAI002
