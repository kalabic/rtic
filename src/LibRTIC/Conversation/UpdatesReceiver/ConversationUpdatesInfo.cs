namespace LibRTIC.Conversation.UpdatesReceiver;

public enum ConversationReceiverState
{
    Connected,
    FinishAfterResponse,
    Disconnecting,
    Disconnected
}

public class ConversationUpdatesInfo
{
    public int nInputAudioCleared = 0;
    public int nResponseStarted = 0;
    public int nResponseFinished = 0;
    public int nSpeechStarted = 0;
    public int nSpeechFinished = 0;
    public int nStreamingStarted = 0;
    public int nStreamingFinished = 0;
    public int nTranscriptionFailed = 0;
    public int nTranscriptionFinished = 0;

    public bool SessionStarted = false;
    public bool ResponseStarted = false;
    public bool SpeechStarted = false;
    public bool StreamingStarted = false;
    public bool WaitingTranscription = false;

    public bool Disposed = false;
    public bool InputAudioRunning = false;
    public ConversationReceiverState receiverState = ConversationReceiverState.Disconnected;
}
