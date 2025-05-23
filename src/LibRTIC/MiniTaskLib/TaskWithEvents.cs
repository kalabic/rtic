using LibRTIC.MiniTaskLib.Base;
using LibRTIC.MiniTaskLib.Events;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib;

public abstract class TaskWithEvents : TaskBase
{
    private bool _disposed = false;

    public EventCollection TaskEvents { get { return _taskEvents; } }

    public string TaskLabel { get { return _label; } }



    protected Info _info;

    private object _lock = new object();

    private string _label = "";

    private EventCollection _taskEvents;

    private CancellationTokenSource _cancellationTokenSource;

    private Task _taskComplete;

    private Task? _taskContinueAction = null;

    public TaskWithEvents(Info info)
        : this(info, new CancellationTokenSource())
    { }

    public TaskWithEvents(Info info, CancellationToken cancellation)
        : this(info, CancellationTokenSource.CreateLinkedTokenSource(cancellation))
    { }

    private TaskWithEvents(Info info, CancellationTokenSource cancellation)
        // TODO: Still considering that CancellationToken.None might be better for base class. WIP
        : base(cancellation.Token, TaskCreationOptions.LongRunning)
    {
        _info = info;
        _cancellationTokenSource = cancellation;
        _taskEvents = new(info, "TaskWithEvents Events");
        _taskEvents.EnableInvokeFor<TaskExceptionOccured>();
        _taskEvents.EnableInvokeFor<TaskCancelled>();
        _taskEvents.EnableInvokeFor<TaskCompleted>();

        _taskComplete = ContinueWith( HandleTaskComplete );
    }

    public Task TaskAwaiter() 
    { 
        return (_taskContinueAction is not null) ? _taskContinueAction : this;
    }

    protected CancellationToken GetPrivateCancellationToken()
    {
        return _cancellationTokenSource.Token;
    }

    public void SetLabel(string label)
    {
        _label = label;
        _taskEvents.Label += " - " + label;
    }

    /// <summary>
    /// Hiding inherited member <see cref="Task.Dispose()"/> to stop it from using <see cref="GC.SuppressFinalize"/>.
    /// </summary>
    protected new void Dispose()
    {
        Dispose(true);
    }

    override protected void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            lock (_lock)
            {
                if (!_disposed)
                {
                    _disposed = true;
                }

                _taskEvents.Dispose();
                _cancellationTokenSource.Dispose();
            }
        }

        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    /// <summary>
    /// <see cref="_taskContinueAction"/> will be run asychronously by a new task after this task is completed.
    /// <para>TODO: Should be easy to enable multiple actions because it is based on chainining tasks with
    /// continuations.</para>
    /// </summary>
    /// <param name="finishAction"></param>
    public void StartAndFinishWithAction(Action finishAction)
    {
        _taskContinueAction = _taskComplete.ContinueWith( (_) => finishAction() );
        Start();
    }

    public TaskWithEvents StopDeviceTask(CancellationToken stopActionCancellation)
    {
        var stopTask = new ActionTask(_info, (actionCancellation) =>
        {
            try
            {
                Cancel();
                if (!IsCompleted)
                {
                    Wait(actionCancellation);
                }
            }
            catch (OperationCanceledException ex)
            {
                _info.ExceptionOccured(ex);
            }
            catch (AggregateException ex)
            {
                _info.ExceptionOccured(ex);
            }
        }, stopActionCancellation);
        stopTask.Start();
        return stopTask;
    }

    //
    // Start() -> StopDeviceTask() {
    //     Cancel() -> Wait() -> Dispose()
    // }
    //

    public void Cancel()
    {
        bool wasCanceled = false;
        lock(_lock)
        {
            if (!_disposed && !IsCompleted && !_cancellationTokenSource.IsCancellationRequested)
            {
                wasCanceled = true;
                _cancellationTokenSource.Cancel();
            }
        }

        if (wasCanceled)
        {
            InvokeTaskEvent(new TaskCancelled());
        }
    }

    //
    // Entry for main task function
    //
    override protected void TaskFunction()
    {
        try
        {
            TaskFunction(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            _info.Info("Operation canceled : " + _label);
        }
        catch (Exception ex)
        {
            NotifyExceptionOccurred(ex);
        }
    }

    private void HandleTaskComplete(Task task)
    {
        InvokeTaskEvent(new TaskCompleted());
        if (!_disposed)
        {
            Dispose();
        }
    }

    private void InvokeTaskEvent<TMessage>(TMessage message)
    {
        lock (_lock)
        {
            if (!_taskEvents.IsComplete)
            {
                _taskEvents.Invoke<TMessage>(message);
            }
        }
    }

    protected abstract void TaskFunction(CancellationToken cancellation);

    virtual protected void NotifyExceptionOccurred(Exception ex)
    {
        _info.ExceptionOccured(ex);
        InvokeTaskEvent(new TaskExceptionOccured(ex));
    }
}
