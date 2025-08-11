using LibRTIC.BasicDevices;
using LibRTIC.BasicDevices.RTIC;

namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

public class RTICmdLineState_Answering : RTIConsoleStateWithTimer
{
    public override RTIConsoleStateId State { get { return RTIConsoleStateId.Answering; } }

    private int _onTimerCount = 0;

    public RTICmdLineState_Answering(ISystemConsole? cout = null)
        : base(cout)
    { }

    override protected void OnTimer(Object? source, System.Timers.ElapsedEventArgs e)
    {
        if (_onTimerCount == 4)
        {
            COWrite("\r     \r");
            _onTimerCount = 0;
        }
        else
        {
            COWrite(".");
            _onTimerCount++;
        }
    }

    override public void Enter()
    {
        _timer.Start();
    }

    override public void Exit()
    {
        COWrite("\r     \r");
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
    }
}
