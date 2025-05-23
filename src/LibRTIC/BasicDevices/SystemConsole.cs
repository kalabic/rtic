namespace LibRTIC.BasicDevices;

/// <summary>
/// A final and direct text output for a conversation session. Writes text
/// without any modifications, formating, replacing, etc.
/// </summary>
public class SystemConsole : ISystemConsole
{
    public void SetCursorLeft(int value)
    {
        Console.CursorLeft = value;
    }

    public void Write(string? message)
    {
        Console.Write(message);
    }

    public void WriteLine()
    {
        Console.WriteLine();
    }

    public void WriteLine(string? message)
    {
        Console.WriteLine(message);
    }

    public void WriteNotification(string message)
    {
        WriteLine(message);
    }
}
