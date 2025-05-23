using Microsoft.Extensions.Logging;
using LibRTIC.BasicDevices.RTIC;
using SIPSorcery.SIP.App;

namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

public class RTICallState_WritingItem : RTICallStateBase
{
    public override RTIConsoleStateId State { get { return RTIConsoleStateId.WritingItem; } }

    public RTICallState_WritingItem(ILogger logger, SIPUserAgent ua, SIPServerUserAgent uas, IMediaSession mediaSession)
       : base(logger, ua, uas, mediaSession)
    { }

    override public void Enter() { }

    override public void Exit() { }

    override public IRTICallState ProcessSessionEvent(RTISessionEventId messageType, IRTICallStateCollection stateCollection)
    {
        switch (messageType)
        {
            case RTISessionEventId.ItemFinished:
                return stateCollection.State_WaitingItem;

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
