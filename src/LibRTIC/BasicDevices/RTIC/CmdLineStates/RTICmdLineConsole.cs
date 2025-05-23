using MiniRTICallServer.RTISorcery.RTICallSessionConsole;

namespace LibRTIC.BasicDevices.RTIC.CmdLineStates;

public class RTICmdLineConsole
{
    static public RTIConsole New(ISystemConsole? cout = null)
    {
        var inactive = new RTICmdLineState_Inactive(cout);
        var connecting = new RTICmdLineState_Connecting(cout);
        var answering = new RTICmdLineState_Answering(cout);
        var waitingItem = new RTICmdLineState_WaitingItem(cout);
        var writingItem = new RTICmdLineState_WritingItem(cout);
        var result = new RTIConsole(inactive, connecting, answering, waitingItem, writingItem, cout);
        return result;
    }
}
