using Microsoft.Extensions.Logging;
using LibRTIC.BasicDevices.RTIC;

namespace MiniRTICallServer.RTISorcery.RTICallSessionConsole;

/// <summary>
/// </summary>
public class RTICallConsole
    : IRTICallStateCollection
    , IRTICallConsole
{
    public ILogger? Log
    {
        get { return _currentState.Log; }
        set 
        {
            lock(_locker)
            {
                _inactive.Log = value;
                _connecting.Log = value;
                _waitingItem.Log = value;
                _writingItem.Log = value;
            }
        }
    }

    public event EventHandler<RTIConsoleStateId>? StateUpdate;

    public IRTICallState State_CurrentState { get { return _currentState; } }

    public IRTICallState State_Inactive { get { return _inactive; } }

    public IRTICallState State_Answering { get { return _answering; } }

    public IRTICallState State_Connecting { get { return _connecting; } }

    public IRTICallState State_WaitingItem { get { return _waitingItem; } }

    public IRTICallState State_WritingItem { get { return _writingItem; } }


    protected object _locker = new object();

    protected IRTICallState? _fixedState = null;

    protected IRTICallState _currentState;

    protected RTICallStateBase _inactive;

    protected RTICallStateBase _connecting;

    protected RTICallStateBase _answering;

    protected RTICallStateBase _waitingItem;

    protected RTICallStateBase _writingItem;

    public RTICallConsole(RTICallStateBase inactive,
                          RTICallStateBase connecting,
                          RTICallStateBase answering,
                          RTICallStateBase waitingItem,
                          RTICallStateBase writingItem,
                          ILogger? cout = null)
    {
        _inactive = inactive;
        _connecting = connecting;
        _answering = answering;
        _waitingItem = waitingItem;
        _writingItem = writingItem;

        _currentState = _inactive;
    }

    public void SetFixedState(IRTICallState value)
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
                Log?.LogError("Session event received in fixed state.");
                return;
            }

            IRTICallState nextState = _currentState.ProcessSessionEvent(sessionEvent, this);
            if (nextState != _currentState)
            {
                ChangeState(nextState);
            }
        }
    }

    protected void ChangeState(IRTICallState nextState)
    {
        _currentState.Exit();
        _currentState = nextState;
        _currentState.Enter();
        StateUpdate?.Invoke(this, _currentState.State);
    }

    //
    // INotificationWriter interface
    //

    public void WriteNotification(string message)
    {
        throw new NotImplementedException();
    }

    //
    // IRTIOutput interface
    //

    public void Write(RTMessageType type, string message) { }

    public void WriteLine(RTMessageType type, string? message) { }

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
#if DEBUG_VERBOSE
        ProcessSessionEvent(RTISessionEventId.ItemStarted, message);
#else
        ProcessSessionEvent(RTISessionEventId.ItemStarted, null);
#endif
    }

    public void ItemFinished(string? message = null)
    {
        ProcessSessionEvent(RTISessionEventId.ItemFinished, null);
    }
}
