//
// For testing purposes, program can be built with or without RUN_SYNC (start as 'void Main()' or 'async Task Main()').
//

//#define RUN_SYNC

using LibRTIC.Config;
using LibRTIC.Conversation;
using LibRTIC.BasicDevices.RTIC;
using LibRTIC.MiniTaskLib.Events;

namespace MiniRTIC;


/// <summary>
/// A Minimum viable RealTime Interactive Console for connecting to OpenAI's realtime API.
/// <para>Please provide one of following in your environment variables:</para>
/// <list type = "bullet">
///   <item>AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT with AZURE_OPENAI_USE_ENTRA=true or AZURE_OPENAI_API_KEY</item>
///   <item>OPENAI_API_KEY</item>
/// </list>
/// </summary>
public partial class Program
{
    /// <summary>
    /// Connected to <see cref="Console.CancelKeyPress"/> inside <see cref="InitializeEnvironment"/> mehod.
    /// </summary>
    static private readonly CancellationTokenSource exitSource = new CancellationTokenSource();

    static private RTICConversationControl? conversationControl = null;

#if RUN_SYNC
    public static void Main(string[] args)
#else
#pragma warning disable CS1998 // CS1998: This async method lacks 'await' operators and will run synchronously.
    public static async Task Main(string[] args)
#pragma warning restore CS1998 // CS1998: This async method lacks 'await' operators and will run synchronously.
#endif
    {
        var exit = exitSource.Token;
        InitializeEnvironment(); // Set UTF-8, handle Ctrl-C, create audio output, etc.

        if (AudioOutput is null || AudioOutput.Microphone is null || AudioOutput.Speaker is null)
        {
            Output.Info.Error("Failed to start audio devices.");
            return;
        }

        // Read client API options from environment variables and nothing else.
        var config = RTICConfig.FromEnvironment();
        if (config is null)
        {
            Output.Info.Error("Failed to read client API options from environment.");
            return;
        }

        RTIConversation conversation = RTIConversationTask.Create(Output.Info, exit);
        conversation.ConfigureWith(config, AudioOutput.Microphone);
        conversationControl = conversation.Control;

        //
        // A collection of events unrelated to conversation itself, but to 'Updates Receiver Task' and other utilities.
        //
        var rev = conversation.ConversationEvents;

        rev.Connect<FailedToConnectMsg>(HandleEvent);
        rev.Connect<FailedToOperateMsg>(HandleEvent);
        rev.Connect<TaskExceptionOccured>( HandleEvent );
        rev.Connect<ClientStartedConnecting>( HandleEvent );

        //
        // A collection of conversation events to listen on, invoked from a task that is not used
        // for fetching conversation updates, so it can handle application functions.
        //
        var cev = conversation.UpdatesReceiverEvents;

        cev.Connect<RTICInputSpeechStarted>(AudioOutput.HandleEvent);
        cev.Connect<RTICInputSpeechFinished>(AudioOutput.HandleEvent);
        cev.Connect<RTICResponseStarted>(AudioOutput.HandleEvent);

        cev.Connect<RTICSessionCreated>( HandleEvent );
        cev.Connect<ConversationSessionFinished>( HandleEvent );
        cev.Connect<RTICResponseStarted>( HandleEvent );
        cev.Connect<RTICResponseCompleted>( HandleEvent );
        cev.Connect<RTICInputTranscriptionCompleted>( HandleEvent );
        cev.Connect<RTICInputTranscriptionFailed>( HandleEvent );
        cev.Connect<RTICOutputAudioDelta>(HandleEvent);
        cev.Connect<RTICOutputTranscriptDelta>(HandleEvent);
        cev.Connect<RTICOutputTextDelta>(HandleEvent);
        cev.Connect<RTICErrorReceived>(HandleEvent);

        var conversationTask = conversation.RunAsync();

        // TODO: Async version of the last part:
        try
        {
            while (!exit.IsCancellationRequested)
            {
                var keyProps = WaitForKey(exit);
                if (keyProps.KeyChar == 'q')
                {
                    Output.Info.Info("User quits.");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Output.Info.Error("Unhandled exception in MiniRTIC.", ex);
        }

        conversation.Cancel();
        conversationTask.Wait();

        AudioOutput.Dispose();
        conversation.Dispose();
    }

    private static void HandleEvent(object? s, FailedToConnectMsg update)
    {
        exitSource.Cancel();
        Output.Event.ConnectingFailed(update.Message);
    }

    private static void HandleEvent(object? s, FailedToOperateMsg update)
    {
        exitSource.Cancel();
        Output.Event.OperationFailed(update.Message);
    }

    private static void HandleEvent(object? s, TaskExceptionOccured update)
    {
        Output.Info.Error("Conversation task exception.", update.Exception);
    }

    private static void HandleEvent(object? s, ClientStartedConnecting update)
    {
        Output.Event.ConnectingStarted();
    }

    /// <summary>
    /// Session started.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private static void HandleEvent(object? s, RTICSessionCreated update)
    {
        // Notify console output that session has started.
        Output.Event.SessionStarted(" *\n * Session started\n * Press 'q' to quit.\n *");
        _ = conversationControl?.RequestResponseAsync(
            new RTICResponseRequest("Greet the caller briefly and ask how you can help."), CancellationToken.None);
    }

    /// <summary>
    /// Session finished.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private static void HandleEvent(object? s, ConversationSessionFinished update) 
    { 
        Output.Event.SessionFinished(" *\n * Session finished\n *\n");

        if (!exitSource.IsCancellationRequested)
        {
            // A case of session being cancelled because of server or network issues (or even a bug in client code).
            // Use main cancellation token source to signal main program to exit.
            exitSource.Cancel();
        }
    }

    /// <summary>
    /// Response started.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private static void HandleEvent(object? s, RTICResponseStarted update)
    {
        Output.Event.ItemStarted(update.ResponseId);
    }

    /// <summary>
    /// Response finished.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private static void HandleEvent(object? s, RTICResponseCompleted update)
    {
        string status = update.Response.Status.ToString().ToLowerInvariant();
        Output.Event.ItemFinished(status);
        if (!update.IsCompleted)
        {
            string? error = update.Response.StatusDetails?.ErrorMessage;
            Output.WriteLine(
                RTMessageType.System,
                $"Response {update.ResponseId} ended with status {status}." +
                $"{(string.IsNullOrWhiteSpace(error) ? string.Empty : $" {error}")}");
        }
    }

    /// <summary>
    /// Complete transcription of user's speech.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private static void HandleEvent(object? s, RTICInputTranscriptionCompleted update)
    {
        if (!String.IsNullOrEmpty(update.Transcript))
        {
            Output.WriteLine(RTMessageType.User, update.Transcript);
        }
    }

    /// <summary>
    /// Transcription of user's speech has failed.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private static void HandleEvent(object? s, RTICInputTranscriptionFailed update)
    {
        if (!String.IsNullOrEmpty(update.ErrorMessage))
        {
            Output.WriteLine(RTMessageType.System, update.ErrorMessage);
        }
        else
        {
            Output.WriteLine(RTMessageType.System, "[Transcription Failed]");
        }
    }

    /// <summary>
    /// This update brings text and audio from AI agent.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private static void HandleEvent(object? s, RTICOutputAudioDelta update)
    {
        var data = update.Audio.ToArray();
        AudioOutput?.Speaker?.Write(data, 0, data.Length);
    }

    /// <summary>
    /// This update brings text and audio from AI agent.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private static void HandleEvent(object? s, RTICOutputTranscriptDelta update)
    {
        if (!String.IsNullOrEmpty(update.Delta))
        {
            Output.Write(RTMessageType.Agent, update.Delta);
        }
    }

    private static void HandleEvent(object? s, RTICOutputTextDelta update)
    {
        if (!String.IsNullOrEmpty(update.Delta))
        {
            Output.Write(RTMessageType.Agent, update.Delta);
        }
    }

    private static void HandleEvent(object? s, RTICErrorReceived update)
    {
        Output.WriteLine(
            RTMessageType.System,
            $"Realtime error ({update.Error.Code ?? update.Error.Kind}): {update.Error.Message}");
    }
}
