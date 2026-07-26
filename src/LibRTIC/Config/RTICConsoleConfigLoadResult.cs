namespace LibRTIC.Config;

/// <summary>
/// Result of loading an authoritative console host entry file
/// (<c>rtic_console.yaml</c>) that composes provider/session paths into <see cref="RTICConfig"/>.
/// </summary>
public sealed class RTICConsoleConfigLoadResult
{
    public RTICConfig? Config { get; }

    public RTICConsoleAppOptions App { get; }

    /// <summary>Absolute path to the API/provider YAML after resolution, when known.</summary>
    public string? ResolvedApiConfigPath { get; }

    /// <summary>Absolute path to the session YAML after resolution, when known.</summary>
    public string? ResolvedSessionConfigPath { get; }

    public IReadOnlyList<ConfigDiagnostic> Diagnostics { get; }

    public bool IsSuccess
        => Config is not null && Diagnostics.All(d => d.Severity != ConfigDiagnosticSeverity.Error);

    internal RTICConsoleConfigLoadResult(
        RTICConfig? config,
        RTICConsoleAppOptions app,
        string? resolvedApiConfigPath,
        string? resolvedSessionConfigPath,
        List<ConfigDiagnostic> diagnostics)
    {
        Config = config;
        App = app ?? throw new ArgumentNullException(nameof(app));
        ResolvedApiConfigPath = resolvedApiConfigPath;
        ResolvedSessionConfigPath = resolvedSessionConfigPath;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Wraps a provider/session-only load (legacy CLI / env) with default host app options.
    /// </summary>
    public static RTICConsoleConfigLoadResult FromProviderLoad(RTICConfigLoadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new RTICConsoleConfigLoadResult(
            result.Config,
            RTICConsoleAppOptions.Default,
            resolvedApiConfigPath: null,
            resolvedSessionConfigPath: null,
            result.Diagnostics.ToList());
    }
}
