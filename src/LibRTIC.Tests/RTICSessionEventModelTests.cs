using AudioFormatLib;
using LibRTIC.Conversation;
using LibRTIC.Realtime;
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
    public void AudioPacketStorageIsExposedWithoutAnotherCopy()
    {
        AudioPacket packet = RealtimeAudioContract.CreatePacket([1, 2, 3, 4]);
        RTICOutputAudioDelta update = new(
            "response_1",
            "item_1",
            0,
            0,
            packet);

        packet.AsValues<short>().Values[0] = 99;

        Assert.Equal(99, update.Audio.AsValues<short>().Values[0]);
    }

    [Fact]
    public void AudioPacketEventsRejectWrongFormatAndUninitializedPackets()
    {
        AudioPacket uninitialized = default;
        AudioPacket wrongFormat = new(
            new ASampleFormat(AValueFormat.S16, 16000, 1),
            1);

        Assert.Throws<ArgumentException>(() => new RTICOutputAudioDelta(
            "response_1", "item_1", 0, 0, uninitialized));
        Assert.Throws<ArgumentException>(() => new RTICOutputAudioDelta(
            "response_1", "item_1", 0, 0, wrongFormat));
    }

    [Fact]
    public void RealtimePacketFactoryRejectsIncompleteSamples()
    {
        Assert.Throws<ArgumentException>(
            () => RealtimeAudioContract.CreatePacket([1, 2, 3]));
    }

    [Fact]
    public void RealtimePacketFactoryCreatesInitializedEmptyPacket()
    {
        AudioPacket packet = RealtimeAudioContract.CreatePacket([]);

        Assert.True(packet.IsInitialized);
        Assert.Equal(0, packet.SampleCount);
        Assert.Equal(0, packet.SampleCapacity);
    }

    [Fact]
    public void AudioContentPartSharesPacketStorage()
    {
        AudioPacket packet = RealtimeAudioContract.CreatePacket([1, 2]);
        var part = new RTICAudioContentPart(packet, "hello", false);

        packet.AsValues<short>().Values[0] = 42;

        Assert.Equal(
            42,
            part.Audio!.Value.AsValues<short>().Values[0]);
    }

}
