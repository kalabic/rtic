namespace LibRTIC.BasicDevices;

/// <summary>
/// Implemented by classes that are final and direct text output for a conversation session.
/// Writes text without any modifications, formating, replacing, etc.
/// </summary>
public interface ISystemConsole
{
    public void SetCursorLeft(int value);

    public void Write(string? message);

    public void WriteLine();

    public void WriteLine(string? message);

    public void WriteNotification(string message);
}
