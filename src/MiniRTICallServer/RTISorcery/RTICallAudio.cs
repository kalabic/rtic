using AudioFormatLib;
using AudioFormatLib.Buffers;
using AudioFormatLib.IO;
using LibRTIC.BasicDevices;
using LibRTIC.Conversation;
using LibRTIC.MiniTaskLib;
using LibRTIC.MiniTaskLib.Model;
using MiniRTICallServer.RTISorcery.RTICallSessionConsole;

namespace MiniRTICallServer.RTISorcery;

public class RTICallAudio : RTIConsoleAudio
{
    public override IAudioBufferInput Speaker { get { return _responseBuffer.Input.Buffer; } }

    public override IAudioBufferOutput SpeakerOutput { get { return _responseBuffer.Output.Buffer; } }

    public override IAudioBufferOutput Microphone { get { return _speechInput.Output.Buffer; } }

    public override IAudioBufferInput MicrophoneInput { get { return _speechInput.Input.Buffer; } }

    public override float Volume
    {
        get { return 0.0f; }

        set { }
    }

    private AudioStreamBuffer _speechInput;

    private AudioStreamBuffer _responseBuffer;

    public RTICallAudio(Info info, AFrameFormat audioFormat)
        : base(info, audioFormat, CancellationToken.None)
    {
        ABufferParams speechBP = new ABufferParams(audioFormat);
        speechBP.BufferSize = (int)audioFormat.BufferSizeFromSeconds(5);
        _speechInput = new AudioStreamBuffer(speechBP);

        ABufferParams responseBP = new ABufferParams(audioFormat);
        responseBP.BufferSize = (int)audioFormat.BufferSizeFromSeconds(60 * 5);
        _responseBuffer = new AudioStreamBuffer(responseBP, CancellationToken.None);
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
