using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.BasicDevices;

/// <summary>
/// Write various notifications to conversation console. Errors, exception information, etc.
/// </summary>
public class ConsoleNotification : Info, IDisposable
{
    static public ConsoleNotification StatDevOut
    {
        get
        {
            if (_staticNotifications is null)
            {
                _staticNotifications = new ConsoleNotification();
            }

            return _staticNotifications;
        }
    }

    static protected ConsoleNotification? _staticNotifications = null;



    private object _locker = new object();

    public ISystemConsole? Writer { get { return _writer; } set { lock (_locker) { _writer = value; } } }

    private ISystemConsole? _writer = null;

    public ConsoleNotification()
    { }

    public ConsoleNotification(ISystemConsole writer)
    {
        _writer = writer;
    }

    public void Dispose()
    {
        lock (_locker)
        {
            _writer = null;
        }
    }

    /// <summary>
    /// It is likely that this class will be invoked from various threads/tasks, so make writing to console thread safe.
    /// </summary>
    /// <param name="text"></param>
    private void LockWrite(string text)
    {
        lock(_locker)
        {
            _writer?.WriteNotification(text);
        }
    }

    public void ExceptionOccured(Exception ex)
    {
        var text = " >>> Exception occured: " + ex.GetType().ToString() + "; Message: " + ex.Message;
        LockWrite(text);
    }

    public void ObjectDisposed(string label)
    {
        var text = " >>> Disposed: " + label;
        LockWrite(text);
    }

    public void ObjectDisposed(object obj)
    {
        var text = " >>> Disposed: " + obj.GetType().ToString();
        LockWrite(text);
    }

    public void TaskFinished(string label, object obj)
    {
        var text = " >>> Task finished: '" + label + "' " + obj.GetType().ToString();
        LockWrite(text);
    }

    public void Error(string errorMessage)
    {
        var text = " >>> Error: " + errorMessage;
        LockWrite(text);
    }

    public void Info(string infoMessage)
    {
        var text = " >>> Info: " + infoMessage;
        LockWrite(text);
    }
}
