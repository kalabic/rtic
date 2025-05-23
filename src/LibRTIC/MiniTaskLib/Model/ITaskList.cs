namespace LibRTIC.MiniTaskLib.Model;

public interface ITaskList
{
    public List<TaskWithEvents> GetTaskList();

    public void Await();

    public Task AwaitAsync(CancellationToken finalCancellation);

}
