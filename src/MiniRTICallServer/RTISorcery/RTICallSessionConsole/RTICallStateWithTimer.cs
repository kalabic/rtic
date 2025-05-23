using Microsoft.Extensions.Logging;
using SIPSorcery.SIP.App;
using Timer = System.Timers.Timer;

namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

public abstract class RTICallStateWithTimer : RTICallStateBase
{
    protected Timer _timer;

    public RTICallStateWithTimer(SIPUserAgent ua, SIPServerUserAgent uas, IMediaSession mediaSession)
        : this(null, ua, uas, mediaSession, 0)
    { }

    public RTICallStateWithTimer(ILogger? cout, SIPUserAgent ua, SIPServerUserAgent uas, IMediaSession mediaSession, int timerPeriodMs)
        : base(cout, ua, uas, mediaSession)
    {
        _timer = new();
        _timer.Interval = timerPeriodMs;
        _timer.Elapsed += OnTimer;
        _timer.AutoReset = true;
    }

    protected abstract void OnTimer(Object? source, System.Timers.ElapsedEventArgs e);

    public override void Exit()
    {
        _timer.Stop();
    }
}
