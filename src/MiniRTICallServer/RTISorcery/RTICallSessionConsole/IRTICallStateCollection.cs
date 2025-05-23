namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

/// <summary>
/// A _collection of all states for <see cref="RTIStateConsole"/>.
/// </summary>
public interface IRTICallStateCollection
{
    public IRTICallState State_CurrentState { get; }

    public IRTICallState State_Inactive { get; }

    public IRTICallState State_Connecting { get; }

    public IRTICallState State_Answering { get; }

    public IRTICallState State_WaitingItem { get; }

    public IRTICallState State_WritingItem { get; }
}
