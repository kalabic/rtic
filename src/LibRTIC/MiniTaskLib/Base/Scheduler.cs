using DotBase.Core;
using System.Collections.Concurrent;

namespace LibRTIC.MiniTaskLib.Base;

class Scheduler : DisposableBase
{
    private object _lock = new object();

    private readonly ConcurrentDictionary<Action, ScheduledTask> _scheduledTasks = new ConcurrentDictionary<Action, ScheduledTask>();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_lock)
            {
                foreach (var task in _scheduledTasks.Values )
                {
                    task.Dispose();
                }
                _scheduledTasks.Clear();
            }
        }

        base.Dispose(disposing);
    }

    public void Execute(Action action, int timeoutMs, bool repeat = false)
    {
        lock (_lock)
        {
            if (!IsDisposed)
            {
                var task = new ScheduledTask(action, timeoutMs, repeat);
                if (!repeat)
                {
#pragma warning disable CS8622 // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).
                    task._taskComplete += RemoveTask;
#pragma warning restore CS8622
                }
                _scheduledTasks.TryAdd(action, task);
                task._timer.Start();
            }
        }
    }

    private void RemoveTask(object sender, EventArgs e)
    {
        var task = (ScheduledTask)sender;
#pragma warning disable CS8622 // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).
        task._taskComplete -= RemoveTask;
#pragma warning restore CS8622
        ScheduledTask? deleted;
        _scheduledTasks.TryRemove(task._action, out deleted);
        task.Dispose();
    }
}
