using NAudio.Wave;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.BasicDevices;

public class SpeakerAudioStream : CircularBufferStream
{
    private const int BUFFER_SECONDS = 60 * 5;

    private const int SUSPEND_UNTIL_ENQUED_LENGTHMS = 250;

    private class WaveBufferProvider : IWaveProvider
    {
        private readonly WaveFormat waveFormat;

        private Stream source;

        WaveFormat IWaveProvider.WaveFormat => waveFormat;

        public WaveBufferProvider(Stream source, WaveFormat waveFormat)
        {
            this.source = source;
            this.waveFormat = waveFormat;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = source.Read(buffer, offset, count);
            if (bytesRead < count)
            {
                Array.Fill<byte>(buffer, 0, bytesRead, count - bytesRead);
            }
            return count;
        }
    }

    private WaveBufferProvider provider;

    private WaveOutEvent waveOut;

    private readonly WaveFormat waveFormat;

    private long _suspendUntilBuffered = 0;

    public float Volume
    {
        get { return waveOut.Volume; }
        set { waveOut.Volume = value; }
    }

    public SpeakerAudioStream(Info info,
                              AudioStreamFormat audioFormat,
                              CancellationToken speakerToken)
        : base(info, audioFormat.BufferSizeFromSeconds(BUFFER_SECONDS), speakerToken)
    {
        waveFormat = new
        (
            rate: audioFormat.SamplesPerSecond,
            bits: audioFormat.BitsPerSample,
            channels: audioFormat.ChannelCount
        );
        provider = new WaveBufferProvider(this, waveFormat);
        waveOut = new WaveOutEvent();
        waveOut.Init(provider);
        waveOut.Play();

        // May help with audio stuttering at the beginning of stream.
        _suspendUntilBuffered = audioFormat.BufferSizeFromMiliseconds(SUSPEND_UNTIL_ENQUED_LENGTHMS);
    }

    public SpeakerAudioStream(Info info, AudioStreamFormat audioFormat)
        : this(info, audioFormat, CancellationToken.None)
    { }

    protected override void Dispose(bool disposing)
    {
        // Release managed resources.
        // if (disposing) { }

        // Release unmanaged resources.
        waveOut.Dispose();
        base.Dispose(disposing);
    }

    public override void Close()
    {
        waveOut.Stop();
        base.Close();
    }

    public long GetBufferedMs()
    {
        long bytesBuffered = GetBufferedBytes();
        long bytesPerSample = waveFormat.BitsPerSample / 8;
        long samplesBuffered = bytesBuffered / (waveFormat.Channels * bytesPerSample);
        long miliseconds = (samplesBuffered * 1000) / waveFormat.SampleRate;
        return (miliseconds > 0) ? (miliseconds + 500) : 0;
    }

    public long GetMilisecondPosition()
    {
        long bytePosition = waveOut.GetPosition();
        long bytesPerSample = waveFormat.BitsPerSample / 8;
        long samplePosition = bytePosition / (waveFormat.Channels * bytesPerSample);
        long milisecondPosition = (samplePosition * 1000) / waveFormat.SampleRate;
        return milisecondPosition;
    }

    public override long Length { get => GetBufferedMs(); }

    public override long Position { get => GetMilisecondPosition(); set => throw new NotImplementedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_suspendUntilBuffered <= _totalBytesWritten)
        {
            return base.Read(buffer, offset, count);
        }

        return 0;
    }
}
