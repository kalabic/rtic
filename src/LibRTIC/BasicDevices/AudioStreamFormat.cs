namespace LibRTIC.BasicDevices;

public class AudioStreamFormat
{
    public readonly int SamplesPerSecond;

    public readonly int ChannelCount;

    public readonly int BytesPerSample;

    public readonly int BitsPerSample;

    public AudioStreamFormat(int samplesPerSecond, int channelCount, int bytesPerSample)
    {
        this.SamplesPerSecond = samplesPerSecond;
        this.ChannelCount = channelCount;
        this.BytesPerSample = bytesPerSample;
        this.BitsPerSample = bytesPerSample * 8;
    }

    public int BufferSizeFromSeconds(int seconds)
    {
        return BytesPerSample * SamplesPerSecond * ChannelCount * seconds;
    }

    public int BufferSizeFromMiliseconds(int miliseconds)
    {
        return BytesPerSample * (SamplesPerSecond / 1000) * ChannelCount * miliseconds;
    }
}
