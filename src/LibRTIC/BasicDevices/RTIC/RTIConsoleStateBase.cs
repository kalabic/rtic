namespace LibRTIC.BasicDevices.RTIC;

public abstract class RTIConsoleStateBase : IRTIConsoleState
{
    public abstract RTIConsoleStateId State { get; }

    private object _locker = new();

    public void SetCOut(ISystemConsole? cout) 
    { 
        lock (_locker) 
        { 
            _cout = cout; 
        } 
    }

    private ISystemConsole? _cout = null;

    protected RTIConsoleStateBase(ISystemConsole? cout)
    {
        this._cout = cout;
    }

    public bool HasUserTranscript { get { return !String.IsNullOrEmpty(_userTranscript); } }

    protected string _userTranscript = "";

    public string GetUserTranscript()
    {
        var result = _userTranscript;
        _userTranscript = "";
        return result;
    }

    public void COSetCursorLeft(int value)
    {
        lock (_locker)
        {
            _cout?.SetCursorLeft(value);
        }
    }

    protected void COWrite(string? text)
    {
        lock (_locker)
        {
            _cout?.Write(text);
        }
    }

    protected void COWriteLine()
    {
        lock (_locker)
        {
            _cout?.WriteLine();
        }
    }

    protected void COWriteLine(string? text)
    {
        lock (_locker)
        {
            _cout?.WriteLine(text);
        }
    }

    public abstract void Enter();

    public abstract void Exit();

    public abstract IRTIConsoleState ProcessSessionEvent(RTISessionEventId eventType, IRTIStateCollection stateCollection);

    public abstract void Write(RTMessageType type, string message);

    public abstract void WriteLine(RTMessageType type, string? message);
}
