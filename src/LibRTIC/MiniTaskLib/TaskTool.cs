using System.Diagnostics;

namespace LibRTIC.MiniTaskLib;

public class TaskTool
{
    static public long CancelAndWait(TaskWithEvents? task, int timeoutMs = -1)
    {
        if (task is not null && !task.IsCompleted)
        {
            var taskList = new List<TaskWithEvents> { task };
            return CancelAndWaitAll(taskList, timeoutMs);
        }
        return 0;
    }

    static public long CancelAndWaitAll(List<TaskWithEvents> taskList, int timeoutMs = -1)
    {
        var runningTasks = taskList.FindAll(task => !task.IsCompleted);

        if (runningTasks.Count > 0)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var stopCanceler = new CancellationTokenSource();
            var taskStoppersArray = new TaskWithEvents[runningTasks.Count];
            for (int index = 0; index < runningTasks.Count; index++)
            {
                taskStoppersArray[index] = runningTasks[index].StopDeviceTask(stopCanceler.Token);
            }
            bool result = Task.WaitAll(taskStoppersArray, timeoutMs);
            stopwatch.Stop();
            if (!result)
            {
                stopCanceler.Cancel();
                return -1;
            }

            return stopwatch.ElapsedMilliseconds;
        }

        return 0;
    }

    static public string BuildMultiLineExceptionErrorString(Exception ex)
    {
        string result = "";
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (result.Length > 0)
            {
                result += "\n";
            }
            result += ("Error: " + e.Message);
        }
        return result;
    }
}
