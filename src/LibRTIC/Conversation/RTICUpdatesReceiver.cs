using AudioFormatLib.IO;
using LibRTIC.Config;
using LibRTIC.Conversation.UpdatesReceiver;
using LibRTIC.MiniTaskLib;
using LibRTIC.MiniTaskLib.Queues;

namespace LibRTIC.Conversation;

internal interface RTICUpdatesReceiver : IActionQueue
{
    ConversationReceiverState ReceiverState { get; }

    bool IsWebSocketOpen { get; }

    /// <summary>
    /// Expected to be invoked from its own action queue thread.
    /// </summary>
    EventQueue ReceiverEvents { get; }

    ConversationUpdatesInfo SessionState { get; }

    ConversationCancellation Cancellation { get; }

    void ConfigureWith(RTICConfig options);

    void FinishReceiver();

    void SendInputAudio(IAudioStreamOutput stream, CancellationToken cancellation);

    Task RequestResponseAsync(
        RTICResponseRequest request,
        CancellationToken cancellationToken);

    Task InterruptOutputAsync(
        RTICOutputInterruption request,
        CancellationToken cancellationToken);
}
