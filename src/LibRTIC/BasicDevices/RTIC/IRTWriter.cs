namespace LibRTIC.BasicDevices.RTIC;

/// <summary>
/// Output interface implemented by every state of <see cref="RTIConsole"/>.
/// </summary>
public interface IRTWriter
{
    public void Write(RTMessageType type, string message);

    public void WriteLine(RTMessageType type, string? message);
}
