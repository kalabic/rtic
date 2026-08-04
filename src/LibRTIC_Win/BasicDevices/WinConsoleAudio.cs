using AudioFormatLib;
using AudioFormatLib.IO;
using AudioFormatLib.IO.S16;
using LibRTIC.BasicDevices;
using LibRTIC.Conversation.Devices;
using DotBase.Log;

namespace LibRTIC_Win.BasicDevices;

public class WinConsoleAudio : RTIConsoleAudio
{
    public override IAudioInputs? Speaker { get { return _speaker?.Input; } }

    public override IAudioOutputs? Microphone { get { return _microphone?.Output; } }

    public override IAudioInputs? MicrophoneInput { get { return _microphone?.Input; } }

    public override float Volume
    {
        get { return (_speaker is not null) ? _speaker.Volume : 0.0f; }

        set
        {
            if (_speaker is not null)
            {
                _speaker.Volume = value;
            }
        }
    }

    private SpeakerAudioStream? _speaker = null;

    private MicrophoneAudioStream? _microphone = null;

    public WinConsoleAudio(InfoLog info, ASampleFormat audioFormat, CancellationToken cancellation)
        : base(info, audioFormat, cancellation)
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _speaker?.Dispose();
            _speaker = null;
            _microphone?.Dispose();
            _microphone = null;
        }

        base.Dispose(disposing);
    }

    public override void Start(
        AudioPacket? waitingMusic = null,
        AudioPacket? helloSample = null)
    {
        ABufferParams spkParams = new(_audioFormat);
        spkParams.BufferSize = (int)_audioFormat.BufferSizeFromSeconds(SpeakerAudioStream.BUFFER_SECONDS);
        _speaker = new SpeakerAudioStream(spkParams, _cancellation);

        ABufferParams micParams = new(_audioFormat);
        micParams.BufferSize = (int)_audioFormat.BufferSizeFromSeconds(
            MicrophoneAudioStream.BUFFER_SECONDS);
        _microphone = MicrophoneAudioStream.Create(micParams, _cancellation);

        base.Start(waitingMusic, helloSample);
    }
}
