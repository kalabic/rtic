using DotBase.Core;

namespace LibRTIC.MiniTaskLib.Base;

public abstract class TaskGroupBase : DisposableBase
{
    public abstract List<TaskWithEvents> GetTaskList();

    public abstract void Await();

    public abstract Task AwaitAsync(CancellationToken finalCancellation);

}
