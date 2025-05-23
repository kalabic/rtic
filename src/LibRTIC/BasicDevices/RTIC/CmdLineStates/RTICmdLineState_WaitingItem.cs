namespace LibRTIC.BasicDevices.RTIC.CmdLineStates;

public class RTICmdLineState_WaitingItem : RTIConsoleStateWithTimer
{
    public override RTIConsoleStateId State { get { return RTIConsoleStateId.WaitingItem; } }


    public RTICmdLineState_WaitingItem(ISystemConsole? cout = null)
        : base(cout)
    { }


    protected override void OnTimer(Object? source, System.Timers.ElapsedEventArgs e)
    {
        COWrite(WaitingConsolePrompt.GetProgressPrompt());
    }

    override public void Enter()
    {
        _timer.Start();
    }

    override public void Exit()
    {
        COWrite("\r      \r");
        base.Exit();
    }

    override public IRTIConsoleState ProcessSessionEvent(RTISessionEventId messageType, IRTIStateCollection stateCollection)
    {
        switch (messageType)
        {
            case RTISessionEventId.ItemStarted:
                return stateCollection.State_WritingItem;

            case RTISessionEventId.SessionFinished:
                return stateCollection.State_Inactive;

            default:
                break;
        }

        return stateCollection.State_CurrentState;
    }

    override public void Write(RTMessageType type, string message)
    {
        throw new NotImplementedException();
    }

    override public void WriteLine(RTMessageType type, string? message)
    {
        if (type == RTMessageType.System)
        {
            COWrite("\r     \r");
            COWriteLine(message);
        }
        else if (type == RTMessageType.User && (message is not null))
        {
            _userTranscript += message;
        }
    }
}
