using Microsoft.Extensions.Logging;
using MiniRTICallServer.RTISorcery.RTICallSessionConsole;
using LibRTIC.Config;
using LibRTIC.Conversation;
using LibRTIC.MiniTaskLib.Events;
using SIPSorcery;
using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;

namespace MiniRTICallServer.RTISorcery;

public class RTICMediaSession : VoIPMediaSession
{
    public static RTICMediaSession New(SIPUserAgent ua, SIPServerUserAgent uas, ConversationOptions? conversationOptions, string dst)
    {
        RTICAudioEndPoint audioEP = new RTICAudioEndPoint();
        return new RTICMediaSession(ua, uas, conversationOptions, audioEP);
    }

    private ILogger Log;

    private RTICAudioEndPointInfo _info;

    private RTICallConsole _console;

    private SIPUserAgent _userAgent;

    private SIPServerUserAgent _serverUserAgent;

    private bool _helloResponseReceived = false;

    private ConversationOptions _conversationOptions;

    private RTIConversation _conversation;

    private Task _conversationTask;

    private RTICAudioEndPoint _audioEP;

    public RTICMediaSession(SIPUserAgent ua, SIPServerUserAgent uas, ConversationOptions? co, RTICAudioEndPoint audioEP)
        : base(audioEP.ToMediaEndPoints())
    {
        Log = LogFactory.CreateLogger<RTICMediaSession>();
        _info = new(Log);

        _userAgent = ua;
        _serverUserAgent = uas;
        _audioEP = audioEP;

        _conversationOptions = (co is not null) ? co : ConversationOptions.FromEnvironment();

        _console = RTICallConsoleBuilder.New(Log, ua, uas, this);

        _conversation = RTIConversationTask.Create(_info, CancellationToken.None);
#pragma warning disable CS8604 // Possible null reference argument for parameter.
        _conversation.ConfigureWith(_conversationOptions, audioEP.Microphone);
#pragma warning restore CS8604 // Possible null reference argument for parameter.

        var cev = _conversation.ConversationEvents;

        // Connect event handlers.
        cev.Connect<FailedToConnect>(HandleEvent);
        cev.Connect<ClientStartedConnecting>(HandleEvent);
        cev.Connect<InputAudioTaskFinished>(HandleEvent);
        cev.Connect<TaskExceptionOccured>(HandleEvent);
        cev.Connect<ConversationSessionStarted>(HandleEvent);
        cev.Connect<ConversationSessionFinished>(HandleEvent);
        cev.Connect<ConversationSessionConfigured>(HandleEvent);
        cev.Connect<ConversationItemStreamingStarted>(HandleEvent);
        cev.Connect<ConversationItemStreamingFinished>(HandleEvent);
        audioEP.ConnectToConversation(cev, _console);

        _conversationTask = _conversation.RunAsync();
    }

    ~RTICMediaSession()
    {
        Dispose(false);
    }

    public override void Dispose()
    {
        Dispose(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _conversation.Dispose();
            _audioEP.Dispose();
        }
    }

    public override void Close(string reason)
    {
        _conversation.Cancel();
        _conversationTask.Wait();
        base.Close(reason);
        Dispose();
    }

    private void HandleEvent(object? s, InputAudioTaskFinished ev)
    {
        LogDebug("(RTICMediaSession) InputAudioTaskFinished");
    }

    private void HandleEvent(object? s, TaskExceptionOccured ev)
    {
        LogDebug("(RTICMediaSession) TaskExceptionOccured");
    }

    private void HandleEvent(object? s, FailedToConnect ev)
    {
        _console.ConnectingFailed();
        Log.LogError($"Incoming call refused. Realtime API Client failed to connect. Error:{ev.Reason}, Message:{ev.Message}");
        _serverUserAgent.Reject(SIPResponseStatusCodesEnum.InternalServerError, "Realtime Interactive API Error");
    }

    private void HandleEvent(object? s, ClientStartedConnecting ev)
    {
        _console.ConnectingStarted();
        LogDebug("(RTICMediaSession) ClientStartedConnecting");
    }

    private void HandleEvent(object? s, ConversationSessionStarted ev)
    {
        _console.SessionStarted();
        LogDebug("(RTICMediaSession) Session Started.");
    }

    private void HandleEvent(object? s, ConversationSessionConfigured ev)
    {
        LogDebug("(RTICMediaSession) Session Configured.");
    }

    private void HandleEvent(object? s, ConversationSessionFinished ev)
    {
        _console.SessionFinished();
        LogDebug("(RTICMediaSession) ConversationSessionFinished");
    }

    private void HandleEvent(object? s, ConversationItemStreamingStarted ev)
    {
        if (!_helloResponseReceived)
        {
            _helloResponseReceived = true;

            // FYI: 'Answer()' seems to always use synchronized version internally.
            Task<bool> answerTask = _userAgent.Answer(_serverUserAgent, this);
            if (answerTask.IsCompletedSuccessfully)
            {
                bool result = answerTask.Result;
                if (result && _userAgent.IsCallActive)
                {
                    // Nothing to await here, 'Start()' starts our 'RTICAudioEndPoint' immediately, there is nothing to wait for.
                    Start();
                    LogDebug("(RTICMediaSession) Call answered.");
                }
                else
                {
                    _console.ConnectingFailed();
                    LogDebug("(RTICMediaSession) Answer failed.");
                }
            }
            else
            {
                throw new InvalidOperationException("Time to update this.");
            }
        }

        _console.ItemStarted();
        LogDebug("(RTICMediaSession) ConversationItemStreamingStarted");
    }

    private void HandleEvent(object? s, ConversationItemStreamingFinished ev)
    {
        _console.ItemFinished();
        LogDebug("(RTICMediaSession) ConversationItemStreamingFinished");
    }

    private void LogDebug(string message)
    {
#if DEBUG
        Log.LogDebug(message);
#endif
    }
}
