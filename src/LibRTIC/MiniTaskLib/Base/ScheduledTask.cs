namespace LibRTIC.MiniTaskLib.Base;

class ScheduledTask : DisposableBase
{
    internal readonly Action _action;

    internal System.Timers.Timer _timer;

    internal EventHandler? _taskComplete;

    public ScheduledTask(Action action, int timeoutMs, bool repeat)
    {
        _action = action;
        _timer = new System.Timers.Timer() { Interval = timeoutMs };
        _timer.AutoReset = repeat;
        _timer.Elapsed += TimerElapsed;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Elapsed -= TimerElapsed;
            _timer.Dispose();
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            _timer = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        base.Dispose(disposing);
    }

    private void TimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _action();
        if (_taskComplete is not null)
        {
            _taskComplete(this, EventArgs.Empty);
        }
    }
}
