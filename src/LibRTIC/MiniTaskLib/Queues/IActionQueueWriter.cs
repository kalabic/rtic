using DotBase.Event;

namespace LibRTIC.MiniTaskLib.Queues;


/// <summary>
/// Posts actions to an action queue and can complete the queue after a final action.
/// </summary>
public interface IActionQueueWriter : IActionDispatcher
{
    public bool IsWriterComplete { get; }

    public bool PostAndComplete(Action action);
}
