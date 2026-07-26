namespace LibRTIC.Config;

/// <summary>
/// AgentCli-style exclusive <c>--config</c> / <c>-c</c> detection for console hosts.
/// Does not parse YAML; only classifies raw process arguments.
/// </summary>
public static class RTICConsoleHostArguments
{
    public const string ConfigOptionLong = "--config";
    public const string ConfigOptionShort = "-c";

    public const string DefaultConsoleEntryFileName = "rtic_console.yaml";

    public enum Mode
    {
        /// <summary>No exclusive config option; use legacy CLI / auto / environment loading.</summary>
        Legacy,

        /// <summary>Exclusive authoritative host entry: <c>--config path</c> alone.</summary>
        ExclusiveConfig,

        /// <summary><c>--config</c> present but not used exclusively or path missing.</summary>
        Invalid,
    }

    /// <summary>
    /// Classifies <paramref name="args"/> for exclusive config mode.
    /// When <see cref="Mode.ExclusiveConfig"/>, <paramref name="configPath"/> is the path token (not yet validated as a file).
    /// When <see cref="Mode.Invalid"/>, <paramref name="errorMessage"/> explains the problem.
    /// </summary>
    public static Mode Classify(string[] args, out string? configPath, out string? errorMessage)
    {
        configPath = null;
        errorMessage = null;

        if (args is null || args.Length == 0)
        {
            return Mode.Legacy;
        }

        bool anyConfig = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (IsConfigOption(args[i]))
            {
                anyConfig = true;
                break;
            }
        }

        if (!anyConfig)
        {
            return Mode.Legacy;
        }

        if (args.Length == 2 && IsConfigOption(args[0]))
        {
            string path = args[1];
            if (string.IsNullOrWhiteSpace(path) || IsConfigOption(path))
            {
                errorMessage = "Option '--config' / '-c' must be supplied by itself with exactly one path.";
                return Mode.Invalid;
            }

            configPath = path;
            return Mode.ExclusiveConfig;
        }

        errorMessage = "Option '--config' / '-c' must be supplied by itself with exactly one path.";
        return Mode.Invalid;
    }

    public static bool IsConfigOption(string token)
        => token is ConfigOptionLong or ConfigOptionShort;
}
