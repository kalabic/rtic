namespace LibRTIC.MiniTaskLib;

/// <summary>
/// Accepts actions that should be executed by another dispatcher, queue, or synchronization context.
/// </summary>
public interface IActionDispatcher
{
    bool IsComplete { get; }

    bool Post(Action action);
}
