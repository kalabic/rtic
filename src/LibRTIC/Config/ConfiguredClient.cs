using Azure.Core;
using Azure.Identity;
using DotBase.Log;
using OpenAI.Realtime;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace LibRTIC.Config;

#pragma warning disable OPENAI001
#pragma warning disable OPENAI002

public class ConfiguredClient
{
    public const string DEFAULT_API_FILENAME = "rtic_api.json";
    private const string OPENAI_REALTIME_MODEL = "gpt-realtime";
    private const string AZURE_AI_SCOPE = "https://ai.azure.com/.default";

    internal sealed class StartedRealtimeSession
    {
        public RealtimeClient Client { get; }
        public RealtimeSessionClient Session { get; }

        public StartedRealtimeSession(RealtimeClient client, RealtimeSessionClient session)
        {
            Client = client;
            Session = session;
        }
    }

    private sealed class ProviderContext
    {
        public RealtimeClient Client { get; }
        public string SessionModel { get; }
        public EndpointType EndpointType { get; }
        public string? ApiKey { get; }
        public DefaultAzureCredential? EntraCredential { get; }

        public ProviderContext(
            RealtimeClient client,
            string sessionModel,
            EndpointType endpointType,
            string? apiKey = null,
            DefaultAzureCredential? entraCredential = null)
        {
            Client = client;
            SessionModel = sessionModel;
            EndpointType = endpointType;
            ApiKey = apiKey;
            EntraCredential = entraCredential;
        }
    }

    public static RealtimeClient? FromOptions(InfoLog info, ClientApiConfig options)
        => CreateProviderContext(info, options)?.Client;

    internal static async Task<StartedRealtimeSession?> StartConversationSessionAsync(
        InfoLog info,
        ClientApiConfig options,
        CancellationToken cancellationToken)
    {
        ProviderContext? context = CreateProviderContext(info, options);
        if (context is null)
        {
            return null;
        }

        RealtimeSessionClientOptions? sessionOptions =
            await GetRealtimeSessionClientOptionsAsync(context, cancellationToken).ConfigureAwait(false);

        RealtimeSessionClient session = await context.Client.StartConversationSessionAsync(
            context.SessionModel,
            sessionOptions,
            cancellationToken).ConfigureAwait(false);

        return new StartedRealtimeSession(context.Client, session);
    }

    private static ProviderContext? CreateProviderContext(InfoLog info, ClientApiConfig options)
    {
        switch (options.Type)
        {
            case EndpointType.AzureOpenAIWithEntra:
                info.Info($"Connecting to Azure OpenAI endpoint (AZURE_OPENAI_ENDPOINT): {options.AOAIEndpoint}");
                info.Info("Using Entra token-based authentication (AZURE_OPENAI_USE_ENTRA).");
                return ForAzureOpenAIWithEntra(options);

            case EndpointType.AzureOpenAIWithKey:
                info.Info($"Connecting to Azure OpenAI endpoint (AZURE_OPENAI_ENDPOINT): {options.AOAIEndpoint}");
                info.Info("Using Azure OpenAI API key authentication (AZURE_OPENAI_API_KEY).");
                return ForAzureOpenAIWithKey(options);

            case EndpointType.OpenAIWithKey:
                info.Info("Connecting to OpenAI endpoint: https://api.openai.com/v1");
                info.Info("Using OpenAI API key authentication (OPENAI_API_KEY).");
                return ForOpenAIWithKey(options);
        }

        info.Error(
            $"Incomplete or missing '{DEFAULT_API_FILENAME}' or environment configuration. Please provide one of:\n"
            + " - AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT with "
            + "AZURE_OPENAI_USE_ENTRA=true or AZURE_OPENAI_API_KEY\n"
            + " - OPENAI_API_KEY");
        return null;
    }

    private static ProviderContext ForAzureOpenAIWithEntra(ClientApiConfig options)
    {
        DefaultAzureCredential credential = new();
        BearerTokenPolicy authenticationPolicy = new(credential, AZURE_AI_SCOPE);
        RealtimeClient client = new(
            authenticationPolicy,
            new RealtimeClientOptions
            {
                Endpoint = GetAzureOpenAIV1Endpoint(options.AOAIEndpoint),
            });

        return new ProviderContext(
            client,
            options.AOAIDeployment,
            EndpointType.AzureOpenAIWithEntra,
            entraCredential: credential);
    }

    private static ProviderContext ForAzureOpenAIWithKey(ClientApiConfig options)
    {
        ApiKeyAuthenticationPolicy authenticationPolicy =
            ApiKeyAuthenticationPolicy.CreateHeaderApiKeyPolicy(
                new ApiKeyCredential(options.AOAIApiKey), "api-key", string.Empty);

        RealtimeClient client = new(
            authenticationPolicy,
            new RealtimeClientOptions
            {
                Endpoint = GetAzureOpenAIV1Endpoint(options.AOAIEndpoint),
            });

        return new ProviderContext(
            client,
            options.AOAIDeployment,
            EndpointType.AzureOpenAIWithKey,
            apiKey: options.AOAIApiKey);
    }

    private static ProviderContext ForOpenAIWithKey(ClientApiConfig options)
    {
        RealtimeClient client = new(new ApiKeyCredential(options.OAIApiKey));
        return new ProviderContext(client, OPENAI_REALTIME_MODEL, EndpointType.OpenAIWithKey);
    }

    private static async Task<RealtimeSessionClientOptions?> GetRealtimeSessionClientOptionsAsync(
        ProviderContext context,
        CancellationToken cancellationToken)
    {
        switch (context.EndpointType)
        {
            case EndpointType.AzureOpenAIWithKey:
            {
                RealtimeSessionClientOptions options = new();
                options.Headers["api-key"] = context.ApiKey!;
                return options;
            }

            case EndpointType.AzureOpenAIWithEntra:
            {
                AccessToken token = await context.EntraCredential!.GetTokenAsync(
                    new TokenRequestContext([AZURE_AI_SCOPE]),
                    cancellationToken).ConfigureAwait(false);

                RealtimeSessionClientOptions options = new();
                options.Headers["Authorization"] = $"Bearer {token.Token}";
                return options;
            }

            default:
                return null;
        }
    }

    private static Uri GetAzureOpenAIV1Endpoint(string aoaiEndpoint)
    {
        string endpoint = aoaiEndpoint.TrimEnd('/');
        if (!endpoint.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            endpoint += "/openai/v1";
        }

        return new Uri(endpoint + "/");
    }
}
