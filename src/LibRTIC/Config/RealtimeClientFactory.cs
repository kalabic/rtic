using Azure.Core;
using Azure.Identity;
using OpenAI.Realtime;
using System.ClientModel;
using System.ClientModel.Primitives;

using LibRTIC.Config;

namespace LibRTIC.Realtime;

#pragma warning disable OPENAI001
#pragma warning disable OPENAI002

public static class RealtimeClientFactory
{
    private const string OpenAiRealtimeModel = "gpt-realtime";
    private const string AzureAiScope = "https://ai.azure.com/.default";

    internal sealed class StartedRealtimeSession(RealtimeClient client, RealtimeSessionClient session)
    {
        public RealtimeClient Client { get; } = client;
        public RealtimeSessionClient Session { get; } = session;
    }

    public static RealtimeClient Create(RTICProviderOptions provider) => CreateContext(provider).Client;

    internal static async Task<StartedRealtimeSession> StartConversationSessionAsync(RTICProviderOptions provider, CancellationToken cancellationToken)
    {
        ProviderContext context = CreateContext(provider);
        RealtimeSessionClientOptions? sessionOptions = await CreateSessionClientOptionsAsync(context, cancellationToken).ConfigureAwait(false);
        RealtimeSessionClient session = await context.Client.StartConversationSessionAsync(context.Model, sessionOptions, cancellationToken).ConfigureAwait(false);
        return new StartedRealtimeSession(context.Client, session);
    }

    private static ProviderContext CreateContext(RTICProviderOptions provider) => provider switch
    {
        OpenAIProviderOptions openAi => new(new RealtimeClient(new ApiKeyCredential(openAi.ApiKey)), OpenAiRealtimeModel, null, null),
        AzureOpenAIApiKeyProviderOptions azure => CreateAzureWithKey(azure),
        AzureOpenAIEntraProviderOptions azure => CreateAzureWithEntra(azure),
        _ => throw new ArgumentOutOfRangeException(nameof(provider)),
    };

    private static ProviderContext CreateAzureWithKey(AzureOpenAIApiKeyProviderOptions options)
    {
        var authentication = ApiKeyAuthenticationPolicy.CreateHeaderApiKeyPolicy(new ApiKeyCredential(options.ApiKey!), "api-key", string.Empty);
        return new ProviderContext(new RealtimeClient(authentication, new RealtimeClientOptions { Endpoint = AzureV1(options.Endpoint) }), options.Deployment, options.ApiKey, null);
    }
    private static ProviderContext CreateAzureWithEntra(AzureOpenAIEntraProviderOptions options)
    {
        DefaultAzureCredential credential = new();
        return new ProviderContext(new RealtimeClient(new BearerTokenPolicy(credential, AzureAiScope), new RealtimeClientOptions { Endpoint = AzureV1(options.Endpoint) }), options.Deployment, null, credential);
    }
    private static async Task<RealtimeSessionClientOptions?> CreateSessionClientOptionsAsync(ProviderContext context, CancellationToken cancellationToken)
    {
        if (context.ApiKey is not null) { var options = new RealtimeSessionClientOptions(); options.Headers["api-key"] = context.ApiKey; return options; }
        if (context.Credential is null) return null;
        AccessToken token = await context.Credential.GetTokenAsync(new TokenRequestContext([AzureAiScope]), cancellationToken).ConfigureAwait(false);
        var entraOptions = new RealtimeSessionClientOptions(); entraOptions.Headers["Authorization"] = $"Bearer {token.Token}"; return entraOptions;
    }
    private static Uri AzureV1(Uri endpoint)
    {
        string value = endpoint.AbsoluteUri.TrimEnd('/');
        if (!value.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase)) value += "/openai/v1";
        return new Uri(value + "/");
    }
    private sealed record ProviderContext(RealtimeClient Client, string Model, string? ApiKey, DefaultAzureCredential? Credential);
}
