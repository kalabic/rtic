using AudioFormatLib.IO;
using LibRTIC.Config;
using LibRTIC.Conversation.UpdatesReceiver;
using LibRTIC.MiniTaskLib;
using LibRTIC.MiniTaskLib.MessageQueue;

namespace LibRTIC.Conversation;

internal interface RTICUpdatesReceiver : IEventMailbox, IDisposable
{
    ConversationReceiverState ReceiverState { get; }

    bool IsWebSocketOpen { get; }

    /// <summary>
    /// Expected to be invoked from its own message queue thread.
    /// </summary>
    EventQueue ReceiverEvents { get; }

    ConversationUpdatesInfo SessionState { get; }

    ConversationCancellation Cancellation { get; }

    void ConfigureWith(RTICConfig options);

    void FinishReceiver();

    void SendInputAudio(IAudioStreamOutput stream, CancellationToken cancellation);

    Task StartResponseAsync(string? instructions, CancellationToken cancellationToken);

    Task InterruptResponseAsync(CancellationToken cancellationToken);

    Task TruncateOutputItemAsync(
        string itemId,
        int contentIndex,
        TimeSpan audioEndTime,
        CancellationToken cancellationToken);
}
