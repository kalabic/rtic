using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Realtime;
using OpenAI;
using System.ClientModel;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.Config;

#pragma warning disable OPENAI002

public class ConfiguredClient
{
    public const string DEFAULT_CONVERSATIONAPI_FILENAME = "realtime_api.conf";

    public static RealtimeClient? FromOptions(Info info, ClientApiConfig options)
    {
        switch (options.Type)
        {
            case EndpointType.AzureOpenAIWithEntra:
                return ForAzureOpenAIWithEntra(info, options.AOAIEndpoint);

            case EndpointType.AzureOpenAIWithKey:
                return ForAzureOpenAIWithKey(info, options.AOAIEndpoint, options.AOAIApiKey);

            case EndpointType.OpenAIWithKey:
                return ForOpenAIWithKey(info, options.OAIApiKey);
        }

        info.Error(
                    $"Incomplete or missing '" + DEFAULT_CONVERSATIONAPI_FILENAME + "' or environment configuration.Please provide one of:\n"
                    + " - AZURE_OPENAI_ENDPOINT with AZURE_OPENAI_USE_ENTRA=true or AZURE_OPENAI_API_KEY\n"
                    + " - OPENAI_API_KEY");
        return null;
    }

    private static RealtimeClient ForAzureOpenAIWithEntra(Info info, string aoaiEndpoint)
    {
        info.Info($" * Connecting to Azure OpenAI endpoint (AZURE_OPENAI_ENDPOINT): {aoaiEndpoint}");
        info.Info($" * Using Entra token-based authentication (AZURE_OPENAI_USE_ENTRA)");

        AzureOpenAIClient aoaiClient = new(new Uri(aoaiEndpoint), new DefaultAzureCredential());
        return aoaiClient.GetRealtimeClient();
    }

    private static RealtimeClient ForAzureOpenAIWithKey(Info info, string aoaiEndpoint, string aoaiApiKey)
    {
        info.Info($" * Connecting to Azure OpenAI endpoint (AZURE_OPENAI_ENDPOINT): {aoaiEndpoint}");
        info.Info($" * Using API key (AZURE_OPENAI_API_KEY): {aoaiApiKey[..5]}**");

        AzureOpenAIClient aoaiClient = new(new Uri(aoaiEndpoint), new ApiKeyCredential(aoaiApiKey));
        return aoaiClient.GetRealtimeClient();
    }

    private static RealtimeClient ForOpenAIWithKey(Info info, string oaiApiKey)
    {
        string oaiEndpoint = "https://api.openai.com/v1";
        info.Info($" * Connecting to OpenAI endpoint (OPENAI_ENDPOINT): {oaiEndpoint}");
        info.Info($" * Using API key (OPENAI_API_KEY): {oaiApiKey[..5]}**");

        OpenAIClient aoaiClient = new(new ApiKeyCredential(oaiApiKey));
        return aoaiClient.GetRealtimeClient();
    }
}
