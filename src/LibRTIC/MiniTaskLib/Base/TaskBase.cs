using System.Reflection;

namespace LibRTIC.MiniTaskLib.Base;

//
// Source: https://stackoverflow.com/a/56489462
//
public abstract class TaskBase : Task
{
#pragma warning disable CS8601 // Possible null reference assignment.
    readonly static FieldInfo m_action =
       typeof(Task).GetField("m_action", BindingFlags.Instance | BindingFlags.NonPublic);
#pragma warning restore CS8601 // Possible null reference assignment.

#pragma warning disable CS8603 // Possible null reference return.
    readonly static Action _dummy = () => { };
#pragma warning restore CS8603 // Possible null reference return.

    public TaskBase(CancellationToken ct, TaskCreationOptions opts)
        : base(_dummy, ct, opts)
    {
        m_action.SetValue(this, (Action)TaskFunction);
    }

    public TaskBase(CancellationToken ct)
        : this(ct, TaskCreationOptions.None)
    { }

    public TaskBase(TaskCreationOptions opts)
        : this(default, opts)
    { }

    public TaskBase()
        : this(default, TaskCreationOptions.None)
    { }

    protected abstract void TaskFunction();
}

public abstract class TaskBase<TArguments> : Task
{
#pragma warning disable CS8601 // Possible null reference assignment.
    readonly static FieldInfo m_action =
       typeof(Task).GetField("m_action", BindingFlags.Instance | BindingFlags.NonPublic);
#pragma warning restore CS8601 // Possible null reference assignment.

#pragma warning disable CS8603 // Possible null reference return.
    readonly static Action _dummy = () => { };
#pragma warning restore CS8603 // Possible null reference return.

    public TArguments TaskArguments { get { return _taskArguments; } }

    protected TArguments _taskArguments;

    public TaskBase(CancellationToken ct, TaskCreationOptions opts, TArguments args)
        : base(_dummy, ct, opts)
    {
        m_action.SetValue(this, (Action)TaskBaseFunctionEntry);
        this._taskArguments = args;
    }

    public TaskBase(CancellationToken ct, TArguments args)
        : this(ct, TaskCreationOptions.None, args)
    { }

    public TaskBase(TaskCreationOptions opts, TArguments args)
        : this(default, opts, args)
    { }

    public TaskBase(TArguments args)
        : this(default, TaskCreationOptions.None, args)
    { }

    private void TaskBaseFunctionEntry() { TaskFunction(_taskArguments); }

    protected abstract void TaskFunction(TArguments args);
}

//
// Source: https://stackoverflow.com/a/56489462
//
public abstract class FunctionTaskBase<TResult> : Task<TResult>
{
#pragma warning disable CS8601 // Possible null reference assignment.
    readonly static FieldInfo m_action =
       typeof(Task).GetField("m_action", BindingFlags.Instance | BindingFlags.NonPublic);
#pragma warning restore CS8601 // Possible null reference assignment.

#pragma warning disable CS8603 // Possible null reference return.
    readonly static Func<TResult> _dummy = () => default;
#pragma warning restore CS8603 // Possible null reference return.

    public FunctionTaskBase(CancellationToken ct, TaskCreationOptions opts)
        : base(_dummy, ct, opts) =>
            m_action.SetValue(this, (Func<TResult?>)FunctionTask);

    public FunctionTaskBase(CancellationToken ct)
        : this(ct, TaskCreationOptions.None)
    { }

    public FunctionTaskBase(TaskCreationOptions opts)
        : this(default, opts)
    { }

    public FunctionTaskBase()
        : this(default, TaskCreationOptions.None)
    { }

    protected abstract TResult? FunctionTask();
}
