namespace LibRTIC.BasicDevices.RTIC;

/// <summary>
/// A _collection of all states for <see cref="RTIConsole"/>.
/// </summary>
public interface IRTIStateCollection
{
    public IRTIConsoleState State_CurrentState { get; }

    public IRTIConsoleState State_Inactive { get; }

    public IRTIConsoleState State_Connecting { get; }

    public IRTIConsoleState State_Answering { get; }

    public IRTIConsoleState State_WaitingItem { get; }

    public IRTIConsoleState State_WritingItem { get; }
}
