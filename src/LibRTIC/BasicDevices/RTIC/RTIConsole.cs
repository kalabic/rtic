namespace LibRTIC.BasicDevices.RTIC;

/// <summary>
/// Console output adjusted to format of realtime interactive conversation between a user and an AI agent.
/// </summary>
public class RTIConsole 
    : IRTIStateCollection
    , IRTConsole
    , ISystemConsole
{
    public ISystemConsole? COut 
    { 
        set 
        {
            lock(_locker)
            {
                _inactive.SetCOut(value);
                _connecting.SetCOut(value);
                _waitingItem.SetCOut(value);
                _writingItem.SetCOut(value);
            }
        }
    }

    public event EventHandler<RTIConsoleStateId>? StateUpdate;

    public IRTIConsoleState State_CurrentState { get { return _currentState; } }

    public IRTIConsoleState State_Inactive { get { return _inactive; } }

    public IRTIConsoleState State_Connecting { get { return _connecting; } }

    public IRTIConsoleState State_Answering { get { return _answering; } }

    public IRTIConsoleState State_WaitingItem { get { return _waitingItem; } }

    public IRTIConsoleState State_WritingItem { get { return _writingItem; } }


    protected object _locker = new object();

    protected IRTIConsoleState? _fixedState = null;

    protected IRTIConsoleState _currentState;

    protected RTIConsoleStateBase _inactive;

    protected RTIConsoleStateBase _connecting;

    protected RTIConsoleStateBase _answering;

    protected RTIConsoleStateBase _waitingItem;

    protected RTIConsoleStateBase _writingItem;

    public RTIConsole(RTIConsoleStateBase inactive,
                      RTIConsoleStateBase connecting,
                      RTIConsoleStateBase answering,
                      RTIConsoleStateBase waitingItem,
                      RTIConsoleStateBase writingItem,
                      ISystemConsole? cout = null)
    {
        _inactive = inactive;
        _connecting = connecting;
        _answering = answering;
        _waitingItem = waitingItem;
        _writingItem = writingItem;

        _currentState = _inactive;
    }

    public void SetFixedState(IRTIConsoleState value)
    {
        if (_fixedState is null)
        {
            _fixedState = value;
        }
        else
        {
            throw new InvalidOperationException("Console state already fixed.");
        }
    }

    private void ProcessSessionEvent(RTISessionEventId sessionEvent, string? message)
    {
        lock (_locker)
        {
            if (_fixedState is not null)
            {
                _fixedState.WriteLine(RTMessageType.System, " >>> Session event received in fixed state.");
                return;
            }

            IRTIConsoleState nextState = _currentState.ProcessSessionEvent(sessionEvent, this);
            if (nextState != _currentState)
            {
                ChangeState(nextState);
            }
            if (message is not null)
            {
                _currentState.WriteLine(RTMessageType.System, message);
            }
        }
    }

    protected void ChangeState(IRTIConsoleState nextState)
    {
        bool hasUserTranscript = false;
        if (_currentState == _waitingItem)
        {
            hasUserTranscript = _waitingItem.HasUserTranscript;
        }

        _currentState.Exit();
        _currentState = nextState;
        _currentState.Enter();

        if (hasUserTranscript && (_currentState == _writingItem))
        {
            _writingItem.WriteLine(RTMessageType.User, _waitingItem.GetUserTranscript());
        }

        StateUpdate?.Invoke(this, _currentState.State);
    }

    //
    // IRTWriter interface
    //

    public void Write(RTMessageType type, string message)
    {
        lock (_locker)
        {
            _currentState.Write(type, message);
        }
    }

    public void WriteLine(RTMessageType type, string? message)
    {
        lock (_locker)
        {
            _currentState.WriteLine(type, message);
        }
    }

    //
    // IRTISessionEvents interface
    //

    public void ConnectingStarted(string? message = null)
    {
        ProcessSessionEvent(RTISessionEventId.ConnectingStarted, null);
    }

    public void ConnectingFailed(string? message = null)
    {
        ProcessSessionEvent(RTISessionEventId.ConnectingFailed, message);
    }

    public void SessionStarted(string? message = null)
    {
        ProcessSessionEvent(RTISessionEventId.SessionStarted, message);
    }

    public void SessionFinished(string? message = null)
    {
        ProcessSessionEvent(RTISessionEventId.SessionFinished, message);
    }

    public void ItemStarted(string? message = null)
    {
        ProcessSessionEvent(RTISessionEventId.ItemStarted, null);
    }

    public void ItemFinished(string? message = null)
    {
        ProcessSessionEvent(RTISessionEventId.ItemFinished, null);
    }

    //
    // IDefaultConsoleOutput
    //

    public void SetCursorLeft(int value)
    {
        throw new NotImplementedException();
    }

    public void Write(string? message)
    {
        if (!String.IsNullOrEmpty(message))
        {
            Write(RTMessageType.System, message);
        }
    }

    public void WriteLine()
    {
        WriteLine(RTMessageType.System, "");
    }

    public void WriteLine(string? message)
    {
        WriteLine(RTMessageType.System, message);
    }

    public void WriteNotification(string message)
    {
        WriteLine(RTMessageType.System, message);
    }
}
