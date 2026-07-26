using System.Collections;
using LibRTIC.Conversation;
using Xunit;

namespace LibRTIC.Tests;

public sealed class RTICSessionEventModelTests
{
    [Fact]
    public void EventContractsExposeCompleteCorrelationCursor()
    {
        RTICSessionEvent value = new RTICOutputTextDelta(
            "response_1",
            "item_1",
            3,
            2,
            "hello");

        IRTICContentEvent content = Assert.IsAssignableFrom<IRTICContentEvent>(value);
        Assert.Equal("response_1", content.ResponseId);
        Assert.Equal("item_1", content.ItemId);
        Assert.Equal(3, content.OutputIndex);
        Assert.Equal(2, content.ContentIndex);
        Assert.Equal(RTICEventId.OutputTextDelta, value.EventId);
    }

    [Fact]
    public void EventIdsHaveStableContiguousIntegerValues()
    {
        RTICEventId[] values = Enum.GetValues<RTICEventId>();

        Assert.Equal(45, values.Length);
        Assert.Equal(
            Enumerable.Range(0, values.Length),
            values.Select(static value => (int)value));
        Assert.Equal(RTICEventId.Unknown, values[0]);
    }

    [Fact]
    public void PayloadCollectionsAreCopiedAndReadOnly()
    {
        List<RTICContentPart> source =
        [
            new RTICTextContentPart("first", false),
        ];

        RTICMessageItem item = new("item_1", "completed", "assistant", source);
        source.Add(new RTICTextContentPart("second", false));

        Assert.Single(item.Content);
        Assert.IsAssignableFrom<IList>(item.Content);
        Assert.True(((IList)item.Content).IsReadOnly);
    }

    [Fact]
    public void BinaryPayloadsAreCopied()
    {
        byte[] source = [1, 2, 3];
        RTICOutputAudioDelta update = new(
            "response_1",
            "item_1",
            0,
            0,
            source);

        source[0] = 99;

        Assert.Equal(new byte[] { 1, 2, 3 }, update.Audio.ToArray());
    }

    [Fact]
    public void UnknownVariantsRetainOnlyNeutralDiagnostics()
    {
        RTICUnknownProviderEvent update = new(
            "OpenAI",
            "future.server.event");

        Assert.Equal("OpenAI", update.ProviderName);
        Assert.Equal(RTICEventId.Unknown, update.EventId);
        Assert.DoesNotContain(
            update.GetType().GetProperties(),
            property => property.PropertyType.Namespace?.StartsWith(
                "OpenAI",
                StringComparison.Ordinal) == true);
    }
}
