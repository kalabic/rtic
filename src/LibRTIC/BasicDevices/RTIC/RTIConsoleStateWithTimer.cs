using Timer = System.Timers.Timer;

namespace LibRTIC.BasicDevices.RTIC;

public abstract class RTIConsoleStateWithTimer : RTIConsoleStateBase
{
    protected Timer _timer;
    public RTIConsoleStateWithTimer()
        : this(null)
    { }

    public RTIConsoleStateWithTimer(ISystemConsole? cout)
        : base(cout)
    {
        _timer = new();
        _timer.Interval = 500;
        _timer.Elapsed += OnTimer;
        _timer.AutoReset = true;
    }

    protected abstract void OnTimer(Object? source, System.Timers.ElapsedEventArgs e);

    //
    // IRTIState interface
    //

    public override void Exit()
    {
        _timer.Stop();
    }
}
