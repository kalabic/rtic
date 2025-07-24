using Microsoft.Extensions.Logging;
using LibRTIC.BasicDevices.RTIC;
using SIPSorcery.SIP.App;

namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

public class RTICallState_Inactive : RTICallStateBase
{
    public override RTIConsoleStateId State { get { return RTIConsoleStateId.Inactive; } }


    public RTICallState_Inactive(ILogger logger, SIPUserAgent ua, SIPServerUserAgent uas, IMediaSession mediaSession)
        : base(logger, ua, uas, mediaSession)
    { }

    public override void Enter() { }

    public override void Exit() { }

    public override IRTICallState ProcessSessionEvent(RTISessionEventId messageType, IRTICallStateCollection stateCollection)
    {
        switch (messageType)
        {
            case RTISessionEventId.ConnectingStarted:
                return stateCollection.State_Connecting;

            case RTISessionEventId.SessionStarted:
#if DEBUG
                throw new ArgumentException("State_Inactive: 'SessionStarted' received before 'ConnectingStarted'.");
#else
                return stateCollection.State_WaitingItem;
#endif

            case RTISessionEventId.SessionFinished:
                break;

            default:
#if DEBUG
                Log?.LogDebug("State_Inactive: ignoring message: " + messageType.ToString());
#endif
                break;
        }

        return stateCollection.State_CurrentState;
    }

    public override void Write(RTMessageType type, string message)
    {
        throw new NotImplementedException();
    }

    public override void WriteLine(RTMessageType type, string? message)
    {
        throw new NotImplementedException();
    }
}
