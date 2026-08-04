using AudioFormatLib;
using AudioFormatLib.Buffers;
using AudioFormatLib.IO;
using NAudio.Wave;

namespace LibRTIC_Win.BasicDevices;

public class SpeakerAudioStream
    : AudioStreamBuffer
{
    public const int BUFFER_SECONDS = 60 * 5;

    internal sealed class WaveBufferProvider : IWaveProvider, IDisposable
    {
        private readonly ASampleFormat _format;
        private readonly WaveFormat _waveFormat;
        private IAudioBufferOutput? _source;
        private long _consumedSampleCount;

        WaveFormat IWaveProvider.WaveFormat => _waveFormat;

        internal long ConsumedSampleCount
            => _consumedSampleCount;

        public WaveBufferProvider(
            IAudioBufferOutput source,
            ASampleFormat format,
            WaveFormat waveFormat)
        {
            _source = source;
            _format = format;
            _waveFormat = waveFormat;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            IAudioBufferOutput? source = Volatile.Read(ref _source);
            if (source is null)
            {
                return 0;
            }

            int alignedCount = count - (count % _format.BytesPerSample);
            int bytesRead = source.Read(buffer, offset, alignedCount);
            Array.Clear(buffer, offset + bytesRead, count - bytesRead);
            int samplesRead = bytesRead / _format.BytesPerSample;
            _consumedSampleCount += samplesRead;
            return count;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _source, null);
        }
    }

    private WaveBufferProvider? _provider;
    private readonly WasapiOut _waveOut;
    private readonly WaveFormat _waveFormat;

    internal long ConsumedSampleCount
        => _provider?.ConsumedSampleCount ?? 0;

    internal int BufferedSampleCount => StoredSampleCount;

    public float Volume
    {
        get { return _waveOut.Volume; }
        set { _waveOut.Volume = value; }
    }

    public SpeakerAudioStream(
        ABufferParams bp,
        CancellationToken speakerToken)
        : base(bp, speakerToken)
    {
        _waveFormat = NAudioS16Format.CreateWaveFormat(
            bp.Format,
            nameof(bp));
        _provider = new WaveBufferProvider(
            Output.Buffer,
            Format,
            _waveFormat);
        _waveOut = new WasapiOut();
        _waveOut.Init(_provider);
        _waveOut.Play();
    }

    public SpeakerAudioStream(ABufferParams bp)
        : this(bp, CancellationToken.None)
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseBuffer();
            _waveOut.Dispose();
        }

        base.Dispose(disposing);
    }

    public override void CloseBuffer()
    {
        _waveOut.Stop();
        _provider?.Dispose();
        _provider = null;
        base.CloseBuffer();
    }
}
