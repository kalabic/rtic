using AudioFormatLib;
using AudioFormatLib.Buffers;
using LibRTIC.BasicDevices.RTIC;
using LibRTIC.Conversation;
using LibRTIC.MiniTaskLib.Base;
using LibRTIC.MiniTaskLib.Model;
using Timer = System.Timers.Timer;

namespace LibRTIC.BasicDevices;

/// <summary>
/// Logic for enqueueing waiting music and sending 'hello sample' before conversation starts is here.
/// <para>An abstract base class that expects from derived classes:
/// <list type = "bullet">
///   <item>Access to speaker in form of audio output stream by implementing <see cref="RTIConsoleAudio.Speaker"/></item>
///   <item>Access to microphone in form of audio input stream by implementing <see cref="RTIConsoleAudio.Microphone"/></item>
///   <item>Adjust speaker volume according to value given to <see cref="RTIConsoleAudio.Volume"/></item>
///   <item>If any custom initialization is needed right before streaming is started, then override member <see cref="RTIConsoleAudio.Start"/>.</item>
/// </list></para>
/// <para>It is tightly related to state chanage events triggered by <see cref="RTIConsole"/>.</para>
/// <para>It is adjusting output volume or stopping playback in response to:
/// <list type = "bullet">
///   <item>Server VAD detecting start of user's speech, event <see cref="ConversationInputSpeechStarted"/></item>
///   <item>Server VAD detecting end of user's speech, event <see cref="ConversationInputSpeechFinished"/></item>
///   <item>Response started, event <see cref="ConversationResponseStarted"/></item>
/// </list>
/// </para>
/// </summary>
public abstract class RTIConsoleAudio : DisposableBase
{
    private const int INPUT_AUDIO_WAIT_PERIOD = 500;

    public abstract IStreamBuffer? Speaker { get; }

    public abstract IStreamBuffer? Microphone { get; }

    public virtual float Volume { get; set; }

    protected Info _info;

    protected AFrameFormat _audioFormat;

    protected CancellationToken _cancellation;

    protected RTIConsoleStateId _state = RTIConsoleStateId.Inactive;

    protected Timer? _timer = null;

    protected byte[]? _helloSample = null;

    private byte[] _silenceBuffer;


    public RTIConsoleAudio(Info info,
                           AFrameFormat audioFormat,
                           CancellationToken cancellation)
    {
        this._info = info;
        this._audioFormat = audioFormat;
        this._cancellation = cancellation;

        _silenceBuffer = new byte[audioFormat.BufferSizeFromMiliseconds(100)];
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_timer is not null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }

        base.Dispose(disposing);
    }

    public virtual void Start(byte[]? waitingMusic = null, byte[]? helloSample = null)
    {
        _state = RTIConsoleStateId.Inactive;

        if (waitingMusic is not null && Speaker is not null)
        {
            Speaker.GetOutputStream().Write(waitingMusic, 0, waitingMusic.Length);
        }
        if (helloSample is not null)
        {
            _helloSample = helloSample;
        }
    }

    /// <summary>
    /// Expected to be set as event handler for <see cref="RTIConsole.StateUpdate"/>.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="state"></param>
    public virtual void HandleEvent(object? sender, RTIConsoleStateId state)
    {
        _state = state;
        if (_state == RTIConsoleStateId.Answering && _helloSample is not null)
        {
            Microphone?.ClearBuffer();
            Microphone?.GetOutputStream().Write(_helloSample, 0, _helloSample.Length);
            _helloSample = null;

            _timer = new();
            _timer.Interval = INPUT_AUDIO_WAIT_PERIOD;
            _timer.Elapsed += OnTimer;
            _timer.AutoReset = true;
            _timer.Start();
        }
        else if (_state == RTIConsoleStateId.WritingItem && _timer is not null)
        {
            _timer.Stop();
            _timer.Dispose();
            _timer = null;
        }
    }

    /// <summary>
    /// Server VAD detected start of user's speech, so ratio speaker volume a bit while user is speaking.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    public void HandleEvent(object? s, ConversationInputSpeechStarted update)
    {
        Volume = 0.3f;
    }

    /// <summary>
    /// Server VAD detected end of user's speech, bring back speaker volume to normal level.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    public void HandleEvent(object? s, ConversationInputSpeechFinished update)
    {
        Volume = 1.0f;
    }

    /// <summary>
    /// New conversation response started, so cut playback of previous one if any.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    public void HandleEvent(object? s, ConversationResponseStarted update)
    {
        Speaker?.ClearBuffer();
    }

    /// <summary>
    /// Write small chunks of silence into audio input until first conversation response is received (as response to 'hello sample').
    /// </summary>
    /// <param name="source"></param>
    /// <param name="e"></param>
    protected void OnTimer(Object? source, System.Timers.ElapsedEventArgs e)
    {
        if (_state == RTIConsoleStateId.Answering && _silenceBuffer is not null)
        {
            Microphone?.GetOutputStream().Write(_silenceBuffer, 0, _silenceBuffer.Length);
        }
    }
}
