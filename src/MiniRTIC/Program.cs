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
///   <item>AZURE_OPENAI_ENDPOINT with AZURE_OPENAI_USE_ENTRA=true or AZURE_OPENAI_API_KEY</item>
///   <item>OPENAI_API_KEY</item>
/// </list>
/// </summary>
public partial class Program
{
    /// <summary>
    /// Connected to <see cref="Console.CancelKeyPress"/> inside <see cref="InitializeEnvironment"/> mehod.
    /// </summary>
    static private readonly CancellationTokenSource exitSource = new CancellationTokenSource();

#if RUN_SYNC
    public static void Main(string[] args)
#else
    public static async Task Main(string[] args)
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
        var config = ConversationOptions.FromEnvironment();
        if (config._client is null)
        {
            Output.Info.Error("Failed to read client API options from environment.");
            return;
        }

        RTIConversation conversation = RTIConversationTask.Create(Output.Info, exit);
        conversation.ConfigureWith(config, AudioOutput.Microphone);

        //
        // A collection of events unrelated to conversation itself, but to 'Updates Receiver Task' and other utilities.
        //
        var rev = conversation.ReceiverEvents;

        rev.Connect<FailedToConnect>( HandleEvent );
        rev.Connect<TaskExceptionOccured>( HandleEvent );
        rev.Connect<ClientStartedConnecting>( HandleEvent );

        //
        // A collection of conversation events to listen on, invoked from a task that is not used
        // for fetching conversation updates, so it can handle application functions.
        //
        var cev = conversation.ConversationEvents;

        cev.Connect<ConversationInputSpeechStarted>(AudioOutput.HandleEvent);
        cev.Connect<ConversationInputSpeechFinished>(AudioOutput.HandleEvent);
        cev.Connect<ConversationResponseStarted>(AudioOutput.HandleEvent);

        cev.Connect<ConversationSessionStarted>( HandleEvent );
        cev.Connect<ConversationSessionFinished>( HandleEvent );
        cev.Connect<ConversationResponseStarted>( HandleEvent );
        cev.Connect<ConversationResponseFinished>( HandleEvent );
        cev.Connect<ConversationInputTranscriptionFinished>( HandleEvent );
        cev.Connect<ConversationInputTranscriptionFailed>( HandleEvent );
        cev.Connect<ConversationItemStreamingPartDelta>( HandleEvent );

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
            Output.Info.ExceptionOccured(ex);
        }

        conversation.Cancel();
        conversationTask.Wait();

        AudioOutput.Dispose();
        conversation.Dispose();
    }

    private static void HandleEvent(object? s, FailedToConnect update)
    {
        exitSource.Cancel();
        Output.Event.ConnectingFailed(update.Message);
    }

    private static void HandleEvent(object? s, TaskExceptionOccured update)
    {
        Output.Info.ExceptionOccured(update.Exception);
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
    private static void HandleEvent(object? s, ConversationSessionStarted update)
    {
        // Notify console output that session has started.
        Output.Event.SessionStarted(" *\n * Session started\n * Press 'q' to quit.\n *");
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
    private static void HandleEvent(object? s, ConversationResponseStarted update)
    {
        Output.Event.ItemStarted(null);
    }

    /// <summary>
    /// Response finished.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private static void HandleEvent(object? s, ConversationResponseFinished update) 
    { 
        Output.Event.ItemFinished(); 
    }

    /// <summary>
    /// Complete transcription of user's speech.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private static void HandleEvent(object? s, ConversationInputTranscriptionFinished update)
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
    private static void HandleEvent(object? s, ConversationInputTranscriptionFailed update)
    {
        if (!String.IsNullOrEmpty(update.ErrorMessage))
        {
            Output.WriteLine(RTMessageType.User, update.ErrorMessage);
        }
        else
        {
            Output.WriteLine(RTMessageType.User, "[Transcription Failed]");
        }
    }

    /// <summary>
    /// This update brings text and audio from AI agent.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private static void HandleEvent(object? s, ConversationItemStreamingPartDelta update)
    {
        if (update.Audio is not null)
        {
            AudioOutput?.Speaker?.GetStreamInput().Write(update.Audio);
        }
        if (!String.IsNullOrEmpty(update.Transcript))
        {
            Output.Write(RTMessageType.Agent, update.Transcript);
        }
    }
}
