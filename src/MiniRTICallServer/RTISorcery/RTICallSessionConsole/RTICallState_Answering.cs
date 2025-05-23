using Microsoft.Extensions.Logging;
using LibRTIC.BasicDevices.RTIC;
using SIPSorcery.SIP.App;

namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

public class RTICallState_Answering : RTICallStateBase
{
    public override RTIConsoleStateId State { get { return RTIConsoleStateId.Answering; } }


    public RTICallState_Answering(ILogger logger, SIPUserAgent ua, SIPServerUserAgent uas, IMediaSession mediaSession)
        : base(logger, ua, uas, mediaSession)
    { }

    public override void Enter() { }

    public override void Exit() { }

    override public IRTICallState ProcessSessionEvent(RTISessionEventId messageType, IRTICallStateCollection stateCollection)
    {
        switch (messageType)
        {
            case RTISessionEventId.ItemStarted:
                return stateCollection.State_WritingItem;

            default:
                break;
        }

        return stateCollection.State_CurrentState;
    }

    public override void Write(RTMessageType type, string message) => throw new NotImplementedException();

    public override void WriteLine(RTMessageType type, string? message) => throw new NotImplementedException();
}
