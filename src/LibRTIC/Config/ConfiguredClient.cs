using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Realtime;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;
using DotBase.Log;

namespace LibRTIC.Config;

#pragma warning disable OPENAI001
#pragma warning disable OPENAI002

public class ConfiguredClient
{
    public const string DEFAULT_API_FILENAME = "rtic_api.json";

    public static RealtimeClient? FromOptions(InfoLog info, ClientApiConfig options)
    {
        switch (options.Type)
        {
            case EndpointType.AzureOpenAIWithEntra:
                info.Info($"Connecting to Azure OpenAI endpoint (AZURE_OPENAI_ENDPOINT): {options.AOAIEndpoint}");
                info.Info($"Using Entra token-based authentication (AZURE_OPENAI_USE_ENTRA)");
                return ForAzureOpenAIWithEntra(options.AOAIEndpoint);

            case EndpointType.AzureOpenAIWithKey:
                info.Info($"Connecting to Azure OpenAI endpoint (AZURE_OPENAI_ENDPOINT): {options.AOAIEndpoint}");
                info.Info($"Using API key (AZURE_OPENAI_API_KEY): {options.AOAIApiKey[..5]}**");
                return ForAzureOpenAIWithKey(options.AOAIEndpoint, options.AOAIApiKey);

            case EndpointType.OpenAIWithKey:
                string oaiEndpoint = "https://api.openai.com/v1";
                info.Info($"Connecting to OpenAI endpoint (OPENAI_ENDPOINT): {oaiEndpoint}");
                info.Info($"Using API key (OPENAI_API_KEY): {options.OAIApiKey[..5]}**");
                return ForOpenAIWithKey(options.OAIApiKey);
        }

        info.Error(
                    $"Incomplete or missing '" + DEFAULT_API_FILENAME + "' or environment configuration. Please provide one of:\n"
                    + " - AZURE_OPENAI_ENDPOINT with AZURE_OPENAI_USE_ENTRA=true or AZURE_OPENAI_API_KEY\n"
                    + " - OPENAI_API_KEY");
        return null;
    }

    private static RealtimeClient ForAzureOpenAIWithEntra(string aoaiEndpoint)
    {
        AzureOpenAIClient aoaiClient = new(new Uri(aoaiEndpoint), new DefaultAzureCredential());
        return aoaiClient.GetRealtimeClient();
    }

    private static RealtimeClient ForAzureOpenAIWithKey(string aoaiEndpoint, string aoaiApiKey)
    {
        ApiKeyAuthenticationPolicy authenticationPolicy =
            ApiKeyAuthenticationPolicy.CreateHeaderApiKeyPolicy(
                new ApiKeyCredential(aoaiApiKey), "api-key", string.Empty);

        return new RealtimeClient(authenticationPolicy, new RealtimeClientOptions()
        {
            Endpoint = GetAzureOpenAIV1Endpoint(aoaiEndpoint),
        });
    }

    private static RealtimeClient ForOpenAIWithKey(string oaiApiKey)
    {
        OpenAIClient aoaiClient = new(new ApiKeyCredential(oaiApiKey));
        return aoaiClient.GetRealtimeClient();
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
