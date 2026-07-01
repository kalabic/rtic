using LibRTIC.BasicDevices.RTIC.CmdLineStates;
using LibRTIC.BasicDevices.RTIC;
using DotBase.Log;

namespace LibRTIC_Win.BasicDevices;

public class WinConsoleWriter : IRTOutput
{
    public LiteConsole LiteConsole { get { return _liteConsole; } }

    public IRTSessionEvents Event { get { return _mainConsole.Event; } }

    public InfoLog Info { get { return _mainConsole.Info; } }



    private LiteConsole _liteConsole;

    private RTIConsole _mainConsole;


    public WinConsoleWriter()
    {
        _liteConsole = new();
        _mainConsole = RTICmdLineConsole.New(_liteConsole);
    }

    public void AddStateEventHandler(EventHandler<RTIConsoleStateId> stateUpdate)
    {
        _mainConsole.StateUpdate += stateUpdate;
    }

    public void SetInactiveState()
    {
        _mainConsole.SetFixedState(_mainConsole.State_Inactive);
    }

    public void Write(RTMessageType type, string message)
    {
        ((IRTWriter)_mainConsole).Write(type, message);
    }

    public void WriteLine(RTMessageType type, string? message)
    {
        ((IRTWriter)_mainConsole).WriteLine(type, message);
    }

    public void WriteLine(string? message = null)
    {
        ((IRTWriter)_mainConsole).WriteLine(RTMessageType.System, message);
    }
}
