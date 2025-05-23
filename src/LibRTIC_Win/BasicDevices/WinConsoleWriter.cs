using LibRTIC.BasicDevices.RTIC.CmdLineStates;
using LibRTIC.BasicDevices.RTIC;
using LibRTIC.BasicDevices;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC_Win.BasicDevices;

public class WinConsoleWriter : IRTWriter
{
    public SystemConsole SystemConsole { get { return _sysConsole; } }

    public IRTSessionEvents Event { get { return _mainConsole; } }

    public Info Info { get { return _consoleNotification; } }



    private SystemConsole _sysConsole;

    private RTIConsole _mainConsole;

    private ConsoleNotification _consoleNotification;


    public WinConsoleWriter()
    {
        _sysConsole = new();
        _mainConsole = RTICmdLineConsole.New(_sysConsole);
        _consoleNotification = new(_mainConsole);
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
