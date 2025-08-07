using AudioFormatLib;
using AudioFormatLib.Buffers;
using NAudio.Wave;

namespace LibRTIC_Win.BasicDevices;

public class MicrophoneAudioStream : CircularBufferStream
{
    // For simplicity, to be able to queue 'Hello there' sample in all kinds of scenarios,
    // this is configured to use a static 3-second ring buffer. TODO: Fix this
    public const int BUFFER_SECONDS = 3;

    private WaveInEvent _waveInEvent;

    EventHandler<WaveInEventArgs> handleDataAvailable;

    public MicrophoneAudioStream(AFrameFormat audioFormat, CancellationToken microphoneToken)
        : base((int)audioFormat.BufferSizeFromSeconds(5), microphoneToken)
    {
        _waveInEvent = new()
        {
            WaveFormat = new WaveFormat(audioFormat.SampleRate, audioFormat.SampleFormat.Bits(), audioFormat.ChannelFormat.Count)
        };
        handleDataAvailable = (_, e) =>
        {
            Write(e.Buffer, 0, e.BytesRecorded);
        };
        _waveInEvent.DataAvailable += handleDataAvailable;
        _waveInEvent.StartRecording();
    }

    protected override void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing && (_waveInEvent is not null))
        {
            _waveInEvent.DataAvailable -= handleDataAvailable;
        }

        // Release unmanaged resources.
        _waveInEvent?.Dispose();
        base.Dispose(disposing);
    }

    public override void Close()
    {
        _waveInEvent.StopRecording();
        base.Close();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // For simplicity, block until all requested data is available and do not perform partial reads.
        if (!WaitDataAvailable(count))
        {
            return 0;
        }

        return base.Read(buffer, offset, count);
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;
}
