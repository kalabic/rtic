using Microsoft.Extensions.Logging;
using LibRTIC.BasicDevices.RTIC;
using SIPSorcery.SIP.App;

namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

public class RTICallState_WaitingItem : RTICallStateBase
{
    public override RTIConsoleStateId State { get { return RTIConsoleStateId.WaitingItem; } }


    public RTICallState_WaitingItem(ILogger logger, SIPUserAgent ua, SIPServerUserAgent uas, IMediaSession mediaSession)
        : base(logger, ua, uas, mediaSession)
    { }

    override public void Enter() { }

    override public void Exit() { }

    override public IRTICallState ProcessSessionEvent(RTISessionEventId messageType, IRTICallStateCollection stateCollection)
    {
        switch (messageType)
        {
            case RTISessionEventId.ItemStarted:
                return stateCollection.State_WritingItem;

            case RTISessionEventId.SessionFinished:
                return stateCollection.State_Inactive;

            default:
#if DEBUG
                Log?.LogDebug("State_WaitingItem: ignoring message: " + messageType.ToString());
#endif
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
