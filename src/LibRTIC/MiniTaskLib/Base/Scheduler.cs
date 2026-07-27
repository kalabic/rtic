using DotBase.Core;
using System.Diagnostics;

namespace LibRTIC.MiniTaskLib.Base;

internal sealed class Scheduler : DisposableBase
{
    private readonly object _lock = new();

    private readonly HashSet<ScheduledTask> _scheduledTasks =
        new(ReferenceEqualityComparer.Instance);

    private readonly Action<Exception>? _faultSink;

    private readonly TimeProvider _timeProvider;

    private bool _disposeStarted;

    internal int ScheduledCount
    {
        get
        {
            lock (_lock)
            {
                return _scheduledTasks.Count;
            }
        }
    }

    internal Scheduler(
        TimeProvider timeProvider,
        Action<Exception>? faultSink = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
        _faultSink = faultSink;
    }

    internal ScheduledTask? Execute(
        Action action,
        int timeoutMs,
        bool repeat = false)
    {
        ScheduledTask? task = null;

        try
        {
            lock (_lock)
            {
                if (_disposeStarted || IsDisposed)
                {
                    return null;
                }

                task = new ScheduledTask(
                    action,
                    timeoutMs,
                    repeat,
                    _timeProvider);
                _scheduledTasks.Add(task);
                task.Start(TaskScheduler.Default);
            }
        }
        catch
        {
            if (task is not null)
            {
                lock (_lock)
                {
                    _scheduledTasks.Remove(task);
                }

                task.AbandonAfterStartFailure();
            }

            throw;
        }

        _ = ObserveCompletionAsync(task);
        return task;
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(disposing);
            return;
        }

        ScheduledTask[] tasks;

        lock (_lock)
        {
            if (_disposeStarted)
            {
                return;
            }

            _disposeStarted = true;
            tasks = _scheduledTasks.ToArray();
        }

        foreach (ScheduledTask task in tasks)
        {
            _ = task.RequestCancellation();
        }

        base.Dispose(disposing);
    }

    private async Task ObserveCompletionAsync(ScheduledTask task)
    {
        Exception? fault = null;

        try
        {
            await task.Completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (task.Completion.IsCanceled)
        {
            // Cancellation requested by Scheduler.Dispose is normal completion.
        }
        catch (Exception ex)
        {
            fault = ex;
        }

        try
        {
            await task.CancellationCompletion.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            fault = fault is null
                ? ex
                : new AggregateException(fault, ex);
        }

        lock (_lock)
        {
            _scheduledTasks.Remove(task);
        }

        try
        {
            task.Dispose();
        }
        catch (Exception ex)
        {
            fault = fault is null
                ? ex
                : new AggregateException(fault, ex);
        }

        if (fault is not null)
        {
            ReportFault(fault);
        }
    }

    private void ReportFault(Exception exception)
    {
        if (_faultSink is null)
        {
            return;
        }

        try
        {
            _faultSink(exception);
        }
        catch (Exception sinkException)
        {
            Trace.TraceError(
                "MiniTaskLib scheduler fault sink failed. Schedule fault: {0}. " +
                "Sink fault: {1}.",
                exception,
                sinkException);
        }
    }
}
