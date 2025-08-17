using AudioFormatLib;
using AudioFormatLib.IO;
using LibRTIC.BasicDevices;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC_Win.BasicDevices;

public class WinConsoleAudio : RTIConsoleAudio
{
    public override IAudioBufferInput? Speaker { get { return _speaker?.Input.Buffer; } }

    public override IAudioBufferOutput? Microphone { get { return _microphone?.Output.Buffer; } }

    public override IAudioBufferInput? MicrophoneInput { get { return _microphone?.Input.Buffer; } }

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

    public WinConsoleAudio(Info info, AFrameFormat audioFormat, CancellationToken cancellation) 
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

    public override void Start(byte[]? waitingMusic = null, byte[]? helloSample = null)
    {
        ABufferParams spkParams = new(_audioFormat);
        spkParams.BufferSize = (int)_audioFormat.BufferSizeFromSeconds(SpeakerAudioStream.BUFFER_SECONDS);
        _speaker = new SpeakerAudioStream(spkParams, _cancellation);

        ABufferParams micParams = new(_audioFormat);
        micParams.BufferSize = (int)_audioFormat.BufferSizeFromSeconds(5);
        _microphone = MicrophoneAudioStream.Create(micParams, _cancellation);

        base.Start(waitingMusic, helloSample);
    }
}
