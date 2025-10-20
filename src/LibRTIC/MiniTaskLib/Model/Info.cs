namespace LibRTIC.MiniTaskLib.Model;

public interface Info
{
    void ExceptionOccured(Exception ex);

    void ObjectDisposed(string label);

    void ObjectDisposed(object obj);

    void TaskFinished(string label, object obj);

    void Error(string errorMessage);

    void Info(string infoMessage);
}
