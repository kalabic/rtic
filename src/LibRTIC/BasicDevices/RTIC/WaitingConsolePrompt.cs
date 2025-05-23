namespace LibRTIC.BasicDevices.RTIC;

public class WaitingConsolePrompt
{
    static private int _step = 0;
    static private string[] _waiting = { "[ . ]", "[   ]" };

    static public string GetProgressPrompt()
    {
        _step++;
        var wl = _waiting[_step % 2];
        string result = "\r" + wl + "\r";
        return result;
    }
}
