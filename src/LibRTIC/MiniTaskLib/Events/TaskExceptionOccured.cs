namespace LibRTIC.MiniTaskLib.Events;

public class TaskExceptionOccured
{
    public readonly Exception Exception;

    public TaskExceptionOccured(Exception ex)
    {
        Exception = ex;
    }
}
