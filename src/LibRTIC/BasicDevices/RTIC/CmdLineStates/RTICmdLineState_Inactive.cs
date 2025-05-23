namespace LibRTIC.BasicDevices.RTIC.CmdLineStates;

public class RTICmdLineState_Inactive : RTIConsoleStateBase
{
    public override RTIConsoleStateId State { get { return RTIConsoleStateId.Inactive; } }


    public RTICmdLineState_Inactive(ISystemConsole? cout = null)
        : base(cout)
    { }

    public override void Enter() { }

    public override void Exit() { }

    public override IRTIConsoleState ProcessSessionEvent(RTISessionEventId messageType, IRTIStateCollection stateCollection)
    {
        switch (messageType)
        {
            case RTISessionEventId.ConnectingStarted:
                return stateCollection.State_Connecting;

            case RTISessionEventId.SessionStarted:
                return stateCollection.State_WaitingItem;

            case RTISessionEventId.SessionFinished:
                break;

            default:
                break;
        }

        return stateCollection.State_CurrentState;
    }

    public override void Write(RTMessageType type, string message)
    {
    }

    public override void WriteLine(RTMessageType type, string? message)
    {
        if (type == RTMessageType.System)
        {
            COWriteLine(message);
        }
    }
}
