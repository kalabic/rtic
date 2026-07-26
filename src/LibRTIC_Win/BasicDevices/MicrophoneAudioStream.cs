using AudioFormatLib;
using AudioFormatLib.Buffers;
using NAudio.Wave;

namespace LibRTIC_Win.BasicDevices;

public class MicrophoneAudioStream : AudioStreamBuffer
{
    /// <summary>
    /// Capture ring buffer length in seconds. Sized for the hello sample plus a short
    /// utterance before the Realtime send loop drains frames (aligned with other mic setups).
    /// </summary>
    public const int BUFFER_SECONDS = 5;

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
            WaveFormat = new WaveFormat(bp.Format.SampleRate, bp.Format.SampleValueFormat.Bits(), bp.Format.ChannelLayout.Count)
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
