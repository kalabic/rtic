namespace LibRTIC.BasicDevices.RTIC;

public interface IRTIConsoleState : IRTWriter
{
    public RTIConsoleStateId State { get; }

    public void SetCOut(ISystemConsole? cout);

    public void Enter();

    public void Exit();

    public IRTIConsoleState ProcessSessionEvent(RTISessionEventId eventType, IRTIStateCollection stateCollection);
}
