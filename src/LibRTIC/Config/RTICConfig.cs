namespace LibRTIC.Config;

/// <summary>Validated, immutable configuration for one realtime provider and one session.</summary>
public sealed class RTICConfig
{
    public RTICProviderOptions Provider { get; }
    public RTICSessionOptions Session { get; }

    public RTICConfig(RTICProviderOptions provider, RTICSessionOptions session)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>
    /// Loads a complete configuration from environment variables only.
    /// Returns <see langword="null"/> when the environment is incomplete or invalid.
    /// </summary>
    public static RTICConfig? FromEnvironment()
        => RTICConfigLoader.LoadEnvironment().Config;
}

public sealed class RTICSessionOptions
{
    public string Instructions { get; }
    public int MaxOutputTokens { get; }
    public ServerVadOptions ServerVad { get; }
    public RTICSessionOptions(string instructions, int maxOutputTokens, ServerVadOptions serverVad)
    {
        Instructions = OpenAIProviderOptions.RequireValue(instructions, nameof(instructions));
        if (maxOutputTokens is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(maxOutputTokens));
        MaxOutputTokens = maxOutputTokens;
        ServerVad = serverVad ?? throw new ArgumentNullException(nameof(serverVad));
    }
}

public sealed class ServerVadOptions
{
    public float Threshold { get; }
    public int PrefixPaddingMs { get; }
    public int SilenceDurationMs { get; }
    public ServerVadOptions(float threshold, int prefixPaddingMs, int silenceDurationMs)
    {
        if (float.IsNaN(threshold) || threshold is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(threshold));
        if (prefixPaddingMs is < 0 or > 2000) throw new ArgumentOutOfRangeException(nameof(prefixPaddingMs));
        if (silenceDurationMs is < 0 or > 2000) throw new ArgumentOutOfRangeException(nameof(silenceDurationMs));
        Threshold = threshold;
        PrefixPaddingMs = prefixPaddingMs;
        SilenceDurationMs = silenceDurationMs;
    }
}
