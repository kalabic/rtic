using AudioFormatLib;
using AudioFormatLib.Buffers;
using NAudio.Wave;

namespace LibRTIC_Win.BasicDevices;

public class SpeakerAudioStream : AudioStreamBuffer
{
    public const int BUFFER_SECONDS = 60 * 5;

    private class WaveBufferProvider : IWaveProvider, IDisposable
    {
        private readonly WaveFormat waveFormat;

        private Stream? source;

        WaveFormat IWaveProvider.WaveFormat => waveFormat;

        public WaveBufferProvider(Stream source, WaveFormat waveFormat)
        {
            this.source = source;
            this.waveFormat = waveFormat;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (source is not null)
            {
                int bytesRead = source.Read(buffer, offset, count);
                if (bytesRead < count)
                {
                    Array.Fill<byte>(buffer, 0, bytesRead, count - bytesRead);
                }
                return count;
            }

            // Disposed
            return 0;
        }

        public void Dispose()
        {
            source = null;
        }
    }

    private WaveBufferProvider? provider;

    private WaveOutEvent waveOut;

    private readonly WaveFormat waveFormat;

    public float Volume
    {
        get { return waveOut.Volume; }
        set { waveOut.Volume = value; }
    }

    public SpeakerAudioStream(ABufferParams bp, CancellationToken speakerToken)
        : base(bp, speakerToken)
    {
        waveFormat = new
        (
            rate: bp.Format.SampleRate,
            bits: bp.Format.SampleFormat.Bits(),
            channels: bp.Format.ChannelLayout.Count
        );
        provider = new WaveBufferProvider(Output.Stream, waveFormat);
        waveOut = new WaveOutEvent();
        waveOut.Init(provider);
        waveOut.Play();
    }

    public SpeakerAudioStream(ABufferParams bp)
        : this(bp, CancellationToken.None)
    { }

    protected override void Dispose(bool disposing)
    {
        if (disposing) 
        {
            CloseBuffer();
            waveOut.Dispose();
        }

        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    public override void CloseBuffer()
    {
        waveOut.Stop();
        provider?.Dispose();
        provider = null;
        base.CloseBuffer();
    }
}
