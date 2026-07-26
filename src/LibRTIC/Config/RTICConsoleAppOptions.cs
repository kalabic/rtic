namespace LibRTIC.Config;

/// <summary>
/// Console-host settings from the <c>app</c> block of a host entry file
/// (shared by RTIConsole and WinRTIC).
/// </summary>
public sealed class RTICConsoleAppOptions
{
    public static RTICConsoleAppOptions Default { get; } = new(verbose: false);

    public bool Verbose { get; }

    public RTICConsoleAppOptions(bool verbose)
    {
        Verbose = verbose;
    }
}
