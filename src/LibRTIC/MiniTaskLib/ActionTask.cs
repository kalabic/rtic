using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib;

public class ActionTask : TaskWithEvents
{
    static public ActionTask RunAction(Info info, Action<CancellationToken> action)
    {
        var task = new ActionTask(info, action);
        task.Start();
        return task;
    }

    static public ActionTask RunAction(Info info, string label, Action<CancellationToken> action)
    {
        var task = new ActionTask(info, action);
        task.SetLabel(label);
        task.Start();
        return task;
    }

    private Action<CancellationToken>? _action = null;

    public ActionTask(Info info, Action<CancellationToken> action)
        : base(info)
    {
        this._action = action;
    }

    public ActionTask(Info info, Action<CancellationToken> action, CancellationToken cancellation)
        : base(info, cancellation)
    {
        this._action = action;
    }

    protected override void TaskFunction(CancellationToken cancellation)
    {
        if (_action is not null)
        {
            _action(cancellation);
            _action = null;
        }
    }
}
