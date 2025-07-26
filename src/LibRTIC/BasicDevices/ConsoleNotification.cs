using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.BasicDevices;

/// <summary>
/// Write various notifications to conversation console. Errors, exception information, etc.
/// </summary>
public class ConsoleNotification : Info, IDisposable
{
    private ISystemConsole? _writer = null;

    public ConsoleNotification()
    { }

    public ConsoleNotification(ISystemConsole writer)
    {
        _writer = writer;
    }

    public void Dispose()
    {
        _writer = null;
    }

    public void ExceptionOccured(Exception ex)
    {
        var text = " >>> Exception occured: " + ex.GetType().ToString() + "; Message: " + ex.Message;
        _writer?.WriteNotification(text);
    }

    public void ObjectDisposed(string label)
    {
        var text = " >>> Disposed: " + label;
        _writer?.WriteNotification(text);
    }

    public void ObjectDisposed(object obj)
    {
        var text = " >>> Disposed: " + obj.GetType().ToString();
        _writer?.WriteNotification(text);
    }

    public void TaskFinished(string label, object obj)
    {
        var text = " >>> Task finished: '" + label + "' " + obj.GetType().ToString();
        _writer?.WriteNotification(text);
    }

    public void Error(string errorMessage)
    {
        var text = " >>> Error: " + errorMessage;
        _writer?.WriteNotification(text);
    }

    public void Info(string infoMessage)
    {
        var text = " >>> Info: " + infoMessage;
        _writer?.WriteNotification(text);
    }
}
