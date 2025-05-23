namespace LibRTIC.MiniTaskLib.Model;

public interface Info
{
    public void ExceptionOccured(Exception ex);

    public void ObjectDisposed(string label);

    public void ObjectDisposed(object obj);

    public void TaskFinished(string label, object obj);

    public void Error(string errorMessage);

    public void Info(string infoMessage);
}
