namespace LibRTIC.Config;


public enum RTICProviderType { OpenAI, AzureOpenAIWithApiKey, AzureOpenAIWithEntra }


public abstract class RTICProviderOptions
{
    public abstract RTICProviderType Type { get; }
}


public sealed class OpenAIProviderOptions : RTICProviderOptions
{
    public override RTICProviderType Type => RTICProviderType.OpenAI;
    public string ApiKey { get; }
    public OpenAIProviderOptions(string apiKey) => ApiKey = RequireValue(apiKey, nameof(apiKey));
    public override string ToString() => "OpenAI (API key authentication)";
    internal static string RequireValue(string? value, string name) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("A value is required.", name);
}


public abstract class AzureOpenAIProviderOptions : RTICProviderOptions
{
    public Uri Endpoint { get; }
    public string Deployment { get; }

    protected AzureOpenAIProviderOptions(Uri endpoint, string deployment)
    {
        if (!endpoint.IsAbsoluteUri) throw new ArgumentException("Endpoint must be absolute.", nameof(endpoint));
        Endpoint = endpoint;
        Deployment = OpenAIProviderOptions.RequireValue(deployment, nameof(deployment));
    }
}

public sealed class AzureOpenAIApiKeyProviderOptions : AzureOpenAIProviderOptions
{
    public override RTICProviderType Type => RTICProviderType.AzureOpenAIWithApiKey;
    public string ApiKey { get; }
    public AzureOpenAIApiKeyProviderOptions(Uri endpoint, string deployment, string apiKey)
        : base(endpoint, deployment) => ApiKey = OpenAIProviderOptions.RequireValue(apiKey, nameof(apiKey));
    public override string ToString() => $"Azure OpenAI (API key authentication, {Endpoint})";
}

public sealed class AzureOpenAIEntraProviderOptions : AzureOpenAIProviderOptions
{
    public override RTICProviderType Type => RTICProviderType.AzureOpenAIWithEntra;
    public AzureOpenAIEntraProviderOptions(Uri endpoint, string deployment) : base(endpoint, deployment) { }
    public override string ToString() => $"Azure OpenAI (Entra authentication, {Endpoint})";
}
