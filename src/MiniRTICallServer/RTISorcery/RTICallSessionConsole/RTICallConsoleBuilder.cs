using Microsoft.Extensions.Logging;
using SIPSorcery.SIP.App;

namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

public class RTICallConsoleBuilder
{
    static public RTICallConsole New(ILogger logger, SIPUserAgent userAgent, SIPServerUserAgent serverUserAgent, IMediaSession mediaSession)
    {
        var inactive = new RTICallState_Inactive(logger, userAgent, serverUserAgent, mediaSession);
        var connecting = new RTICallState_Connecting(logger, userAgent, serverUserAgent, mediaSession);
        var answering = new RTICallState_Answering(logger, userAgent, serverUserAgent, mediaSession);
        var waitingItem = new RTICallState_WaitingItem(logger, userAgent, serverUserAgent, mediaSession);
        var writingItem = new RTICallState_WritingItem(logger, userAgent, serverUserAgent, mediaSession);
        var result = new RTICallConsole(inactive, connecting, answering, waitingItem, writingItem, logger);
        return result;
    }
}
