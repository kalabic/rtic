using LibRTIC.MiniTaskLib;

namespace LibRTIC.MiniTaskLib.Model;

/// <summary>
/// Writes actions to an event mailbox and can complete the mailbox after a final action.
/// </summary>
public interface IEventMailboxWriter : IActionDispatcher
{
    public bool IsWriterComplete { get; }

    public bool PostFinal(Action action);
}
