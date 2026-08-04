using AudioFormatLib;
using AudioFormatLib.Buffers;
using AudioFormatLib.IO;
using NAudio.Wave;

namespace LibRTIC_Win.BasicDevices;

public class MicrophoneAudioStream
    : AudioStreamBuffer
{
    /// <summary>
    /// Capture ring buffer length in seconds. Sized for the hello sample plus a short
    /// utterance before the Realtime send loop drains samples (aligned with other mic setups).
    /// </summary>
    public const int BUFFER_SECONDS = 5;

    public static MicrophoneAudioStream Create(ABufferParams bp, CancellationToken microphoneToken)
    {
        // bp.WaitForCompleteRead = true;
        return new MicrophoneAudioStream(bp, microphoneToken);
    }

    private WaveInEvent? _waveInEvent;

    private readonly EventHandler<WaveInEventArgs> _handleDataAvailable;

    private MicrophoneAudioStream(ABufferParams bp, CancellationToken microphoneToken)
        : base(bp, microphoneToken)
    {
        _waveInEvent = new()
        {
            WaveFormat = NAudioS16Format.CreateWaveFormat(
                bp.Format,
                nameof(bp))
        };
        _handleDataAvailable = HandleDataAvailable;
        _waveInEvent.DataAvailable += _handleDataAvailable;
        _waveInEvent.StartRecording();
    }

    private void HandleDataAvailable(object? sender, WaveInEventArgs e)
    {
        IAudioBufferInput input = Input.Buffer;
        input.Write(e.Buffer, 0, e.BytesRecorded);

    }

    protected override void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing && (_waveInEvent is not null))
        {
            CloseBuffer();
            _waveInEvent.DataAvailable -= _handleDataAvailable;
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
