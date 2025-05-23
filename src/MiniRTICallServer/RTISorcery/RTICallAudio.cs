using LibRTIC.BasicDevices.RTIC;
using LibRTIC.BasicDevices;
using LibRTIC.Conversation;
using LibRTIC.MiniTaskLib.Model;
using LibRTIC.MiniTaskLib;
using MiniRTICallServer.RTISorcery.RTICallSessionConsole;

namespace MiniRTICallServer.RTISorcery;

public class RTICallAudio : RTIConsoleAudio
{
    public override ExStream? Speaker { get { return _responseBuffer; } }

    public override ExStream? Microphone { get { return _speechInput; } }

    public override float Volume
    {
        get { return 0.0f; }

        set { }
    }

    private AudioStreamBuffer _speechInput;

    private CircularBufferStream _responseBuffer;

    public RTICallAudio(Info info, AudioStreamFormat audioFormat)
        : base(info, audioFormat, CancellationToken.None)
    {
        _speechInput = new AudioStreamBuffer(_info, audioFormat, 2, CancellationToken.None);
        _responseBuffer = new CircularBufferStream(_info, audioFormat.BufferSizeFromSeconds(60 * 5), CancellationToken.None);
    }

    public override void Dispose()
    {
        _speechInput.Dispose();
        _responseBuffer.Dispose();
    }

    public void ConnectToConversation(EventCollection conversationEvents, RTICallConsole callConsole)
    {
        conversationEvents.Connect<ConversationInputSpeechStarted>(HandleEvent);
        conversationEvents.Connect<ConversationInputSpeechFinished>(HandleEvent);
        conversationEvents.Connect<ConversationResponseStarted>(HandleEvent);
        callConsole.StateUpdate += HandleEvent;
    }

    public override void Start(byte[]? waitingMusic = null, byte[]? helloSample = null)
    {
        _state = RTIConsoleStateId.Inactive;

        if (waitingMusic is not null)
        {
            _responseBuffer.Write(waitingMusic, 0, waitingMusic.Length);
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
        _responseBuffer.ClearBuffer();
    }
}
