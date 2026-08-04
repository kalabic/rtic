using AudioFormatLib;
using AudioFormatLib.IO;
using AudioFormatLib.IO.S16;
using DotBase.Core;
using LibRTIC.BasicDevices.RTIC;
using LibRTIC.Conversation;
using LibRTIC.Conversation.Devices;
using DotBase.Log;
using LibRTIC.Realtime;
using Timer = System.Timers.Timer;

namespace LibRTIC.BasicDevices;

/// <summary>
/// Logic for enqueueing waiting music and sending 'hello sample' before conversation starts is here.
/// <para>An abstract base class that expects from derived classes:
/// <list type = "bullet">
///   <item>Access to the speaker's S16 sample input by implementing <see cref="RTIConsoleAudio.Speaker"/></item>
///   <item>Access to complete microphone S16 samples by implementing <see cref="RTIConsoleAudio.Microphone"/></item>
///   <item>Adjust speaker volume according to value given to <see cref="RTIConsoleAudio.Volume"/></item>
///   <item>If any custom initialization is needed right before streaming is started, then override member <see cref="RTIConsoleAudio.Start"/>.</item>
/// </list></para>
/// <para>It is tightly related to state chanage events triggered by <see cref="RTIConsole"/>.</para>
/// <para>It is adjusting output volume or stopping playback in response to:
/// <list type = "bullet">
///   <item>Server VAD detecting start of user's speech, event <see cref="RTICInputSpeechStarted"/></item>
///   <item>Server VAD detecting end of user's speech, event <see cref="RTICInputSpeechFinished"/></item>
///   <item>Response started, event <see cref="RTICResponseStarted"/></item>
/// </list>
/// </para>
/// </summary>
public abstract class RTIConsoleAudio : DisposableBase
{
    private const int INPUT_AUDIO_WAIT_PERIOD = 500;

    public abstract IAudioInputs? Speaker { get; }

    /// <summary>Captured microphone S16 samples, or <c>null</c> before initialization.</summary>
    public abstract IAudioOutputs? Microphone { get; }

    /// <summary> Enable sending recorded speech or silence. Enable SIP server to forward user speech from SIP client. </summary>
    public virtual IAudioInputs? MicrophoneInput { get { return null; } }

    public virtual float Volume { get; set; }

    protected InfoLog _info;

    protected ASampleFormat _audioFormat;

    protected CancellationToken _cancellation;

    protected RTIConsoleStateId _state = RTIConsoleStateId.Inactive;

    protected Timer? _timer = null;

    protected AudioPacket? _helloSample = null;

    private readonly AudioPacket _silencePacket;

    private float _normalVolume = 0.0f;

    public RTIConsoleAudio(InfoLog info,
                           ASampleFormat audioFormat,
                           CancellationToken cancellation)
    {
        this._info = info;
        this._audioFormat = audioFormat;
        this._cancellation = cancellation;

        int silenceSamples = checked((int)(audioFormat.SampleRate * 100L / 1000L));
        _silencePacket = new AudioPacket(
            audioFormat,
            silenceSamples,
            silenceSamples);
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

    public virtual void Start(
        AudioPacket? waitingMusic = null,
        AudioPacket? helloSample = null)
    {
        _state = RTIConsoleStateId.Inactive;

        if (waitingMusic is AudioPacket music)
        {
            RealtimeAudioContract.ValidatePacket(
                in music,
                nameof(waitingMusic));
            if (Speaker is IAudioInputs speaker)
            {
                speaker.S16Samples?.TryWrite(in music);
            }
        }
        if (helloSample is AudioPacket hello)
        {
            RealtimeAudioContract.ValidatePacket(
                in hello,
                nameof(helloSample));
            _helloSample = hello;
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
        if (_state == RTIConsoleStateId.Answering &&
            _helloSample is AudioPacket helloSample)
        {
            MicrophoneInput?.Buffer.ClearBuffer();
            MicrophoneInput?.S16Samples?.TryWrite(in helloSample);
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
    public void HandleEvent(object? s, RTICInputSpeechStarted update)
    {
        _normalVolume = Volume;
        Volume = 0.3f * _normalVolume;
    }

    /// <summary>
    /// Server VAD detected end of user's speech, bring back speaker volume to normal level.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    public void HandleEvent(object? s, RTICInputSpeechFinished update)
    {
        Volume = _normalVolume;
    }

    /// <summary>
    /// New conversation response started, so cut playback of previous one if any.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    public void HandleEvent(object? s, RTICResponseStarted update)
    {
        ClearSpeaker();
    }

    /// <summary>
    /// Write small chunks of silence into audio input until first conversation response is received (as response to 'hello sample').
    /// </summary>
    /// <param name="source"></param>
    /// <param name="e"></param>
    protected void OnTimer(Object? source, System.Timers.ElapsedEventArgs e)
    {
        if (_state == RTIConsoleStateId.Answering)
        {
            MicrophoneInput?.S16Samples?.TryWrite(in _silencePacket);
        }
    }

    /// <summary>Clears queued speaker audio using the device-specific policy.</summary>
    protected virtual void ClearSpeaker()
    {
        Speaker?.Buffer.ClearBuffer();
    }
}
