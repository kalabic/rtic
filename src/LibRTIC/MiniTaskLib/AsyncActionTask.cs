using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib;

public class AsyncActionTask : TaskWithEvents
{
    private Func<CancellationToken, Task> _action;

    public AsyncActionTask(Info info, Func<CancellationToken, Task> action)
        : base(info)
    {
        this._action = action;
    }

    public AsyncActionTask(Info info, Func<CancellationToken, Task> action, CancellationToken cancellation)
        : base(info, cancellation)
    {
        this._action = action;
    }

    protected override async void TaskFunction(CancellationToken cancellation)
    {
        await _action(cancellation);
    }
}
