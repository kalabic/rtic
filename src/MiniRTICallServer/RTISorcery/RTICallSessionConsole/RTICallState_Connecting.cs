using Microsoft.Extensions.Logging;
using LibRTIC.BasicDevices.RTIC;
using SIPSorcery.SIP.App;

namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

public class RTICallState_Connecting : RTICallStateBase
{
    public override RTIConsoleStateId State { get { return RTIConsoleStateId.Connecting; } }

    public RTICallState_Connecting(ILogger logger, SIPUserAgent ua, SIPServerUserAgent uas, IMediaSession mediaSession)
        : base(logger, ua, uas, mediaSession)
    { }

    override public void Enter() { }

    override public void Exit() { }

    override public IRTICallState ProcessSessionEvent(RTISessionEventId messageType, IRTICallStateCollection stateCollection)
    {
        switch (messageType)
        {
            case RTISessionEventId.ConnectingFailed:
                return stateCollection.State_Inactive;

            case RTISessionEventId.SessionStarted:
                return stateCollection.State_Answering;

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
        throw new NotImplementedException();
    }
}
