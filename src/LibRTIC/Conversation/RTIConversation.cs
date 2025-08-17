using AudioFormatLib.Buffers;
using AudioFormatLib.IO;
using LibRTIC.Config;
using LibRTIC.MiniTaskLib;
using LibRTIC.MiniTaskLib.Base;
using OpenAI.Realtime;

namespace LibRTIC.Conversation;

#pragma warning disable OPENAI002

public abstract class RTIConversation : TaskListBase
{
    /// <summary>
    /// Events unrelated to conversation itself, but to network connection, tools, etc.
    /// <list type = "bullet">
    ///   <item><see cref="ClientStartedConnecting"></item>
    ///   <item><see cref="InputAudioTaskFinished"></item>
    ///   <item><see cref="FailedToConnect"></item>
    /// </list>
    /// </summary>
    public abstract EventCollection ReceiverEvents { get; }

    /// <summary>
    /// Conversation related events.
    /// <list type = "bullet">
    ///   <item><see cref="ConversationSessionFinished"></item>
    ///   <item><see cref="ConversationSessionStarted"></item>
    ///   <item><see cref="ConversationInputAudioCleared"></item>
    ///   <item><see cref="ConversationInputAudioCommitted"></item>
    ///   <item><see cref="ConversationItemCreated"></item>
    ///   <item>etc.</item>
    /// </list>
    /// </summary>
    public abstract EventQueue ConversationEvents { get; }

    public abstract void ConfigureWith(RealtimeClient client, IAudioBufferOutput audioOutputStream);

    public abstract void ConfigureWith(ConversationOptions options, IAudioBufferOutput audioOutputStream);

    public abstract void Run();

    public abstract Task RunAsync();

    public abstract TaskWithEvents? GetAwaiter();

    public abstract void Cancel();
}


/// <summary>
/// Invoked from <see cref="RTIConversation.ReceiverEvents"/>
/// </summary>
public class ClientStartedConnecting
{
    public readonly EndpointType EndpointType;

    public ClientStartedConnecting(EndpointType endpointType)
    {
        this.EndpointType = endpointType;
    }
}

public class InputAudioTaskFinished
{
    public InputAudioTaskFinished() { }
}

public class FailedToConnect
{
    public enum ErrorStatus
    {
        Unknown,
        EndpointOptionsMissing,
        FailedToConfigure,
        ConnectionCanceled,
        ServerDidNotRespond,
    }

    public readonly ErrorStatus Reason;

    public readonly string Message;

    public FailedToConnect(ErrorStatus reason, string message)
    {
        this.Reason = reason;
        this.Message = message;
    }
}


/// <summary>
/// Invoked from <see cref="RTIConversation.ConversationEvents"/>
/// </summary>
public class ConversationSessionFinished { }

public class ConversationSessionStarted { }

public class ConversationInputAudioCleared { }

public class ConversationInputAudioCommitted { }

public class ConversationItemCreated { }

public class ConversationItemDeleted { }

public class ConversationError { }

public class ConversationInputSpeechStarted { }

public class ConversationInputSpeechFinished { }

public class ConversationItemStreamingAudioFinished { }

public interface ConversationInputTranscriptionFailed 
{
    public string ErrorMessage { get; }
}

public interface ConversationInputTranscriptionFinished
{
    public string Transcript { get; }
}

public class ConversationItemStreamingAudioTranscriptionFinished { }

public interface ConversationItemStreamingFinished
{
    public string ItemId { get; }
}

public interface ConversationItemStreamingPartDelta 
{
    public BinaryData Audio { get; }

    public string ItemId { get; }

    public string Transcript { get; }
}

public class ConversationItemStreamingPartFinished { }

public interface ConversationItemStreamingStarted
{
    public string FunctionName { get; }

    public string ItemId { get; }
}

public class ConversationItemStreamingTextFinished { }

public class ConversationRateLimits { }

public class ConversationResponseFinished { }

public class ConversationResponseStarted { }

public class ConversationSessionConfigured { }

public class ConversationItemTruncated { }
