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

    public int ActiveResponseCount = 0;
    public int ActiveSpeechCount = 0;
    public int ActiveStreamingItemCount = 0;
    public int PendingTranscriptionCount = 0;

    public bool Disposed = false;
    public bool InputAudioRunning = false;
    public ConversationReceiverState receiverState = ConversationReceiverState.Disconnected;
}
