namespace LibRTIC.BasicDevices.RTIC.CmdLineStates;

public class RTICmdLineState_WritingItem : RTIConsoleStateWithTimer
{
    public override RTIConsoleStateId State { get { return RTIConsoleStateId.WritingItem; } }


    private bool _waitingTranscript = false;

    private string _agentBuffer = "";

    public RTICmdLineState_WritingItem(ISystemConsole? cout = null)
       : base(cout)
    { }

    protected override void OnTimer(Object? source, System.Timers.ElapsedEventArgs e)
    {
        COWrite(WaitingConsolePrompt.GetProgressPrompt());
    }

    override public void Enter()
    {
        string itemHeader = "[---- " + DateTime.Now.ToLongTimeString() + " ---- " + DateTime.Now.ToShortDateString() + " ----]\n";
        // Align text right.
        COSetCursorLeft(Console.BufferWidth - itemHeader.Length - 5);
        COWriteLine(itemHeader);
        _timer.Start();
        _waitingTranscript = true;
    }

    override public void Exit()
    {
        if (_waitingTranscript)
        {
            _timer.Stop();
            COWrite("\r      \r");
            _waitingTranscript = false;
            COWriteLine(" AGENT: " + _agentBuffer);
            COWriteLine("");
            _agentBuffer = "";
        }
        else
        {
            COWriteLine();
            COWriteLine();
        }
        base.Exit();
    }

    override public IRTIConsoleState ProcessSessionEvent(RTISessionEventId messageType, IRTIStateCollection stateCollection)
    {
        switch (messageType)
        {
            case RTISessionEventId.ItemFinished:
                return stateCollection.State_WaitingItem;

            case RTISessionEventId.SessionFinished: // Unexpected, but happens when various errors occur.
                return stateCollection.State_Inactive;

            default:
#if DEBUG
                COWriteLine("State_WritingItem: ignoring message: " + messageType.ToString());
#endif
                break;
        }

        return stateCollection.State_CurrentState;
    }

    override public void Write(RTMessageType type, string message)
    {
        if (type == RTMessageType.Agent)
        {
            if (_waitingTranscript)
            {
                _agentBuffer += message;
            }
            else
            {
                COWrite(message);
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    override public void WriteLine(RTMessageType type, string? message)
    {
        if (type == RTMessageType.User)
        {
            if (_waitingTranscript)
            {
                COWrite("\r      \r");
                _timer.Stop();
                _waitingTranscript = false;
                COWriteLine("  USER: " + ((message is not null) ? message : ""));
                COWrite(" AGENT: " + _agentBuffer);
                _agentBuffer = "";
            }
            else
            {
                COWriteLine("\n[UNEXPECTED TRANSCRIPT UPDATE]\n");
                COWriteLine("  USER: " + ((message is not null) ? message : ""));
                COWrite(" AGENT: ");
            }
        }
        else if (type == RTMessageType.System)
        {
            if (_waitingTranscript)
            {
                COWrite("\r      \r");
            }
            else
            {
                COWriteLine();
            }

            COWriteLine(message);
        }
#if DEBUG
        else
        {
            throw new NotImplementedException();
        }
#endif
    }
}
