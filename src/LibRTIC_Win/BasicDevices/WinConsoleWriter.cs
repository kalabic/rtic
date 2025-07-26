using LibRTIC.BasicDevices.RTIC.CmdLineStates;
using LibRTIC.BasicDevices.RTIC;
using LibRTIC.BasicDevices;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC_Win.BasicDevices;

public class WinConsoleWriter : IRTOutput
{
    public SystemConsole SystemConsole { get { return _sysConsole; } }

    public IRTSessionEvents Event { get { return _mainConsole.Event; } }

    public Info Info { get { return _mainConsole.Info; } }



    private SystemConsole _sysConsole;

    private RTIConsole _mainConsole;


    public WinConsoleWriter()
    {
        _sysConsole = new();
        _mainConsole = RTICmdLineConsole.New(_sysConsole);
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
