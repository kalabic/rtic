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
#if DEBUG
                throw new ArgumentException("State_Inactive: 'SessionStarted' received before 'ConnectingStarted'.");
#else
                return stateCollection.State_WaitingItem;
#endif

            case RTISessionEventId.SessionFinished:
                break;

            default:
#if DEBUG
                COWriteLine("State_Inactive: ignoring message: " + messageType.ToString());
#endif
                break;
        }

        return stateCollection.State_CurrentState;
    }

    public override void Write(RTMessageType type, string message)
    {
#if DEBUG
        throw new ArgumentException("State_Inactive: Unexpected message type " + type.ToString());
#endif
    }

    public override void WriteLine(RTMessageType type, string? message)
    {
        if (type == RTMessageType.System)
        {
            COWriteLine(message);
        }
#if DEBUG
        else
        {
            throw new ArgumentException("State_Inactive: Unexpected message type " + type.ToString());
        }
#endif
    }
}
