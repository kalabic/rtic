using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib.Base;

public abstract class TaskListBase 
    : DisposableBase
    , ITaskList
{
    public abstract List<TaskWithEvents> GetTaskList();

    public abstract void Await();

    public abstract Task AwaitAsync(CancellationToken finalCancellation);

}
