using System.Reflection;
using System.Runtime.InteropServices;

namespace LibRTIC.MiniTaskLib.Base;

internal static class TaskActionAccessor
{
    private const string ActionFieldName = "m_action";

    private static readonly Lazy<FieldInfo> s_actionField = new(
        ResolveActionField,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static void Set(Task task, Delegate action)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            FieldInfo field = s_actionField.Value;
            field.SetValue(task, action);

            if (!ReferenceEquals(field.GetValue(task), action))
            {
                throw CreateUnsupportedRuntimeException(
                    "The private task action could not be replaced.");
            }
        }
        catch (PlatformNotSupportedException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is ArgumentException
            or FieldAccessException
            or MemberAccessException
            or TargetException)
        {
            throw CreateUnsupportedRuntimeException(
                "The private task action is incompatible with this runtime.",
                ex);
        }
    }

    private static FieldInfo ResolveActionField()
    {
        FieldInfo? field = typeof(Task).GetField(
            ActionFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (field is null)
        {
            throw CreateUnsupportedRuntimeException(
                $"The private Task.{ActionFieldName} field was not found.");
        }

        if (field.IsStatic || field.IsInitOnly)
        {
            throw CreateUnsupportedRuntimeException(
                $"The private Task.{ActionFieldName} field cannot be modified.");
        }

        return field;
    }

    private static PlatformNotSupportedException CreateUnsupportedRuntimeException(
        string reason,
        Exception? innerException = null)
    {
        string message =
            $"{reason} MiniTaskLib TaskBase requires private Task.{ActionFieldName} " +
            $"access. Runtime: {RuntimeInformation.FrameworkDescription}; " +
            $"version: {Environment.Version}.";

        return new PlatformNotSupportedException(message, innerException);
    }
}

//
// Source: https://stackoverflow.com/a/56489462
//
public abstract class TaskBase : Task
{
    private static readonly Action s_dummy = () => { };

    public TaskBase(CancellationToken ct, TaskCreationOptions opts)
        : base(s_dummy, ct, opts)
    {
        TaskActionAccessor.Set(this, (Action)TaskFunction);
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
    private static readonly Action s_dummy = () => { };

    public TArguments TaskArguments { get { return _taskArguments; } }

    protected TArguments _taskArguments;

    public TaskBase(CancellationToken ct, TaskCreationOptions opts, TArguments args)
        : base(s_dummy, ct, opts)
    {
        TaskActionAccessor.Set(this, (Action)TaskBaseFunctionEntry);
        _taskArguments = args;
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
    private static readonly Func<TResult> s_dummy = () => default!;

    public FunctionTaskBase(CancellationToken ct, TaskCreationOptions opts)
        : base(s_dummy, ct, opts) =>
            TaskActionAccessor.Set(this, (Func<TResult?>)FunctionTask);

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
