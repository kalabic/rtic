using Microsoft.Extensions.Logging;
using LibRTIC.BasicDevices.RTIC;

namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

public interface IRTICallState
{
    public RTIConsoleStateId State { get; }

    public ILogger? Log { get; set; }

    public void Enter();

    public void Exit();

    public IRTICallState ProcessSessionEvent(RTISessionEventId eventType, IRTICallStateCollection stateCollection);
}
