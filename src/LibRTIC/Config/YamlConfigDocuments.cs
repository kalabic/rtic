namespace LibRTIC.Config;

// These are deliberately mutable deserialization bindings. They never escape the
// loader: validation turns them into the immutable RTIC* configuration domain.
internal sealed class ApiConfigDocument
{
    public ProviderMapping? Provider { get; set; }

    internal sealed class ProviderMapping
    {
        public string? Type { get; set; }
        public string? Endpoint { get; set; }
        public string? Deployment { get; set; }
        public AuthenticationMapping? Authentication { get; set; }
    }

    internal sealed class AuthenticationMapping
    {
        public string? Type { get; set; }
        public string? ApiKey { get; set; }
    }
}

internal sealed class SessionConfigDocument
{
    public string? Instructions { get; set; }
    public int? MaxOutputTokens { get; set; }
    public ServerVadMapping? ServerVad { get; set; }

    internal sealed class ServerVadMapping
    {
        public float? Threshold { get; set; }
        public int? PrefixPaddingMs { get; set; }
        public int? SilenceDurationMs { get; set; }
    }
}

/// <summary>
/// Host entry document shared by RTIConsole and WinRTIC (<c>rtic_console.yaml</c>).
/// </summary>
internal sealed class ConsoleEntryConfigDocument
{
    public RealtimeClientMapping? RealtimeClient { get; set; }

    public AppMapping? App { get; set; }

    internal sealed class RealtimeClientMapping
    {
        public string? ApiConfigPath { get; set; }
        public string? SessionConfigPath { get; set; }
    }

    internal sealed class AppMapping
    {
        public bool? Verbose { get; set; }
    }
}
