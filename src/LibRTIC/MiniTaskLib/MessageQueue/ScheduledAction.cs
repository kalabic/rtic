using LibRTIC.MiniTaskLib.Base;

namespace LibRTIC.MiniTaskLib.MessageQueue;

public class ScheduledAction : IProcessMessage
{
    private Action _action;

    public ScheduledAction(Action action)
    {
        _action = action;
    }

    public override void ProcessMessage()
    {
        _action();
    }
}
