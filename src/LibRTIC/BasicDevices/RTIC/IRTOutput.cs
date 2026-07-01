using DotBase.Log;

namespace LibRTIC.BasicDevices.RTIC;

/// <summary>
/// Realtime output interface with functions for writing text, information, errors, responding to session events, etc.
/// </summary>
public interface IRTOutput : IRTWriter
{
    public abstract IRTSessionEvents Event { get; }

    public abstract InfoLog Info { get; }
}
