using LibRTIC.Conversation;
using Xunit;

namespace LibRTIC.Tests;

public sealed class RTICSessionEventModelTests
{
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

}
