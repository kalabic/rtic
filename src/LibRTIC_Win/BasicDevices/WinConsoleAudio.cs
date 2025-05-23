using LibRTIC.BasicDevices;
using LibRTIC.BasicDevices.RTIC;
using LibRTIC.Conversation;
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
            if (_firstResponseReceived)
            {
                if (_speaker is not null)
                {
                    _speaker.Volume = value;
                }
            }
        }
    }

    private SpeakerAudioStream? _speaker = null;

    private MicrophoneAudioStream? _microphone = null;

    public WinConsoleAudio(Info info, AudioStreamFormat audioFormat, CancellationToken cancellation) 
        : base(info, audioFormat, cancellation)
    {
    }

    public override void Dispose()
    {
        _speaker?.Dispose();
        _microphone?.Dispose();
    }

    public override void Start(byte[]? waitingMusic = null, byte[]? helloSample = null)
    {
        _state = RTIConsoleStateId.Inactive;
        _speaker = new SpeakerAudioStream(_info, _audioFormat, _cancellation);
        _microphone = new MicrophoneAudioStream(_info, _audioFormat, _cancellation);

        if (waitingMusic is not null)
        {
            _speaker.Write(waitingMusic, 0, waitingMusic.Length);
        }
        if (helloSample is not null)
        {
            _helloSample = helloSample;
        }
    }

    /// <summary>
    /// Server VAD detected start of user's speech.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    public void HandleEvent(object? s, ConversationInputSpeechStarted update)
    {
        // Ratio speaker volume while user is speaking.
        Volume = 0.3f;
    }

    /// <summary>
    /// Server VAD detected end of user's speech.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    public void HandleEvent(object? s, ConversationInputSpeechFinished update)
    {
        Volume = 1.0f;
    }

    /// <summary>
    /// Response started.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    public void HandleEvent(object? s, ConversationResponseStarted update)
    {
        _speaker?.ClearBuffer();
    }
}
