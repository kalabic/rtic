using AudioFormatLib;
using AudioFormatLib.Buffers;
using NAudio.Wave;

namespace LibRTIC_Win.BasicDevices;

public class MicrophoneAudioStream : AudioStreamBuffer
{
    // For simplicity, to be able to queue 'Hello there' sample in all kinds of scenarios,
    // this is configured to use a static 3-second ring buffer. TODO: Fix this
    public const int BUFFER_SECONDS = 3;

    public static MicrophoneAudioStream Create(ABufferParams bp, CancellationToken microphoneToken)
    {
        // bp.WaitForCompleteRead = true;
        return new MicrophoneAudioStream(bp, microphoneToken);
    }

    private WaveInEvent? _waveInEvent;

    EventHandler<WaveInEventArgs> handleDataAvailable;

    private MicrophoneAudioStream(ABufferParams bp, CancellationToken microphoneToken)
        : base(bp, microphoneToken)
    {
        _waveInEvent = new()
        {
            WaveFormat = new WaveFormat(bp.Format.SampleRate, bp.Format.SampleFormat.Bits(), bp.Format.ChannelLayout.Count)
        };
        handleDataAvailable = (_, e) =>
        {
            Input.Stream.Write(e.Buffer, 0, e.BytesRecorded);
        };
        _waveInEvent.DataAvailable += handleDataAvailable;
        _waveInEvent.StartRecording();
    }

    protected override void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing && (_waveInEvent is not null))
        {
            CloseBuffer();
            _waveInEvent.DataAvailable -= handleDataAvailable;
            _waveInEvent.Dispose();
        }

        _waveInEvent = null;
        base.Dispose(disposing);
    }

    public override void CloseBuffer()
    {
        _waveInEvent?.StopRecording();
        base.CloseBuffer();
    }
}
