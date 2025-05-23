using LibRTIC.BasicDevices.RTIC;

namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

/// <summary>
/// Output interface implemented by every state of <see cref="RTIStateConsole"/>.
/// </summary>
public interface IRTICallOutput
{
    public void Write(RTMessageType type, string message);

    public void WriteLine(RTMessageType type, string? message);
}
