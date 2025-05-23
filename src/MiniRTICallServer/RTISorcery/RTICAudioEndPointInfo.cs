using Microsoft.Extensions.Logging;
using LibRTIC.MiniTaskLib.Model;

namespace MiniRTICallServer.RTISorcery;

public class RTICAudioEndPointInfo : Info
{
    private ILogger _logger;

    public RTICAudioEndPointInfo(ILogger logger)
    {
        _logger = logger;
    }

    public void Error(string errorMessage)
    {
        _logger.LogError(errorMessage);
    }

    public void ExceptionOccured(Exception ex)
    {
        _logger.LogError($"Error occured: {ex.Message}");
    }

    public void Info(string infoMessage)
    {
        _logger.LogInformation(infoMessage);
    }

    public void ObjectDisposed(string label)
    {
        _logger.LogDebug("Object disposed: " + label);
    }

    public void ObjectDisposed(object obj)
    {
        _logger.LogDebug("Object disposed: " + obj.GetType().ToString());
    }

    public void TaskFinished(string label, object obj)
    {
        _logger.LogDebug("Task finished: " + label);
    }
}
