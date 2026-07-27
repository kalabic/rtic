using AudioFormatLib.IO;
using DotBase.Event;
using LibRTIC.Config;
using LibRTIC.MiniTaskLib;
using LibRTIC.MiniTaskLib.Base;

namespace LibRTIC.Conversation;

public abstract class RTIConversation : TaskGroupBase
{
    private protected RTIConversation()
    {
        Control = new RTICConversationControl(
            RequestResponseCoreAsync,
            InterruptOutputCoreAsync);
    }

    /// <summary>Provider-neutral commands for the running conversation.</summary>
    public RTICConversationControl Control { get; }

    /// <summary>
    /// Events unrelated to conversation itself, but to network connection, tools, etc.
    /// <list type = "bullet">
    ///   <item><see cref="ClientStartedConnecting"></item>
    ///   <item><see cref="InputAudioTaskFinished"></item>
    ///   <item><see cref="FailedToConnectMsg"></item>
    /// </list>
    /// </summary>
    public abstract EventProducerCollection ConversationEvents { get; }

    /// <summary>
    /// Conversation related events.
    /// <list type = "bullet">
    ///   <item><see cref="ConversationSessionFinished"></item>
    ///   <item><see cref="RTICSessionCreated"></item>
    ///   <item><see cref="RTICInputAudioCleared"></item>
    ///   <item><see cref="RTICInputAudioCommitted"></item>
    ///   <item><see cref="RTICItemCreated"></item>
    ///   <item>etc.</item>
    /// </list>
    /// </summary>
    public abstract EventQueue UpdatesReceiverEvents { get; }

    /// <summary>Configures the session and its mono PCM16 microphone-frame source.</summary>
    public abstract void ConfigureWith(RTICConfig options, IPcm16FrameOutput audioOutputFrames);

    public abstract void Run();

    public abstract Task RunAsync();

    private protected abstract Task RequestResponseCoreAsync(
        RTICResponseRequest request,
        CancellationToken cancellationToken);

    private protected abstract Task InterruptOutputCoreAsync(
        RTICOutputInterruption request,
        CancellationToken cancellationToken);

    public abstract TaskWithEvents? GetAwaiter();

    public abstract void Cancel();
}


/// <summary>
/// Invoked from <see cref="RTIConversation.ConversationEvents"/>
/// </summary>
public class ClientStartedConnecting
{
    public readonly RTICProviderType ProviderType;

    public ClientStartedConnecting(RTICProviderType providerType)
    {
        ProviderType = providerType;
    }
}

public class InputAudioTaskFinished
{
    public InputAudioTaskFinished() { }
}

public class FailedToConnectMsg
{
    public enum ErrorStatus
    {
        Unknown,
        EndpointOptionsMissing,
        FailedToConfigure,
        ConnectionCanceled,
        ServerDidNotRespond,
        StartupTimedOut,
    }

    public readonly ErrorStatus Reason;

    public readonly string Message;

    public FailedToConnectMsg(ErrorStatus reason, string message)
    {
        this.Reason = reason;
        this.Message = message;
    }
}

/// <summary>
/// Client connected, but for some reason connection is not operational (for example, audio related issues).
/// </summary>
public class FailedToOperateMsg
{
    public enum ErrorStatus
    {
        Unknown,
    }

    public readonly ErrorStatus Reason;

    public readonly string Message;

    public FailedToOperateMsg(ErrorStatus reason, string message)
    {
        this.Reason = reason;
        this.Message = message;
    }
}


/// <summary>
/// Invoked from <see cref="RTIConversation.UpdatesReceiverEvents"/>
/// </summary>
public class ConversationSessionFinished { }
