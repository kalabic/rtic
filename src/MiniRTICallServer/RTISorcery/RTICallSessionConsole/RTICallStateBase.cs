using Microsoft.Extensions.Logging;
using LibRTIC.BasicDevices.RTIC;
using SIPSorcery.SIP.App;

namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

public abstract class RTICallStateBase : IRTICallState
{
    public abstract RTIConsoleStateId State { get; }

    private object _locker = new();

    protected SIPUserAgent _userAgent;

    protected SIPServerUserAgent _serverUserAgent;

    protected IMediaSession _mediaSession;

    public ILogger? Log
    {
        get { return _logger; }
        set
        {
            lock (_locker)
            {
                _logger = value;
            }
        }
    }

    protected ILogger? _logger = null;

    protected RTICallStateBase(ILogger? logger, SIPUserAgent ua, SIPServerUserAgent uas, IMediaSession mediaSession)
    {
        _logger = logger;
        _userAgent = ua;
        _serverUserAgent = uas;
        _mediaSession = mediaSession;
    }

    public abstract void Enter();

    public abstract void Exit();

    public abstract IRTICallState ProcessSessionEvent(RTISessionEventId eventType, IRTICallStateCollection stateCollection);

    public abstract void Write(RTMessageType type, string message);

    public abstract void WriteLine(RTMessageType type, string? message);
}
