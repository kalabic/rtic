using LibRTIC.BasicDevices;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC_Win.BasicDevices;

public class WinConsoleAudio : RTIConsoleAudio
{
    public override ExStream? Speaker { get { return _speaker; } }

    public override ExStream? Microphone { get { return _microphone; } }

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

    public WinConsoleAudio(Info info, AudioStreamFormat audioFormat, CancellationToken cancellation) 
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
        _speaker = new SpeakerAudioStream(_info, _audioFormat, _cancellation);
        _microphone = new MicrophoneAudioStream(_info, _audioFormat, _cancellation);
        base.Start(waitingMusic, helloSample);
    }
}
