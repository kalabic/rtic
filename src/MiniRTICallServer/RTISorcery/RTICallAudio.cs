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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _speechInput.Dispose();
            _responseBuffer.Dispose();
        }

        base.Dispose(disposing);
    }

    public void ConnectToConversation(EventCollection conversationEvents, RTICallConsole callConsole)
    {
        conversationEvents.Connect<ConversationInputSpeechStarted>(HandleEvent);
        conversationEvents.Connect<ConversationInputSpeechFinished>(HandleEvent);
        conversationEvents.Connect<ConversationResponseStarted>(HandleEvent);
        callConsole.StateUpdate += HandleEvent;
    }
}
