using AudioFormatLib;
using AudioFormatLib.IO;
using AudioFormatLib.Utils;
using LibRTIC.Config;
using LibRTIC.Conversation;
using LibRTIC.MiniTaskLib;
using Microsoft.Extensions.Logging;
using MiniRTICallServer.RTISorcery.RTICallSessionConsole;
using SIPSorcery;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using System.Diagnostics;
using System.Net;
using Timer = System.Timers.Timer;

namespace MiniRTICallServer.RTISorcery;

#pragma warning disable CS0067 // CS0067: The event <x> is never used


public class RTICAudioEndPoint : IAudioSource, IAudioSink, IDisposable
{
    private const int RESPONSE_AUDIO_INTERVAL = 200;

    public static readonly AudioSamplingRatesEnum DefaultAudioSourceSamplingRate = AudioSamplingRatesEnum.Rate8KHz;

    public static readonly AudioSamplingRatesEnum DefaultAudioPlaybackRate = AudioSamplingRatesEnum.Rate8KHz;

    //
    // Public properties
    //

    public IAudioBufferOutput Microphone { get { return _audio.Microphone; } }

    public event EncodedSampleDelegate? OnAudioSourceEncodedSample;

    public event Action<EncodedAudioFrame> OnAudioSourceEncodedFrameReady;


    //
    // Private
    //

    private int _timerLock = 0;

    private int _encodedMediaLock = 0;

    private ILogger _logger = LogFactory.CreateLogger<RTICAudioEndPoint>();

    private RTICAudioEndPointInfo _info;

    protected Timer _responseTimer;

    private IAudioEncoder _audioEncoder;

    private MediaFormatManager<AudioFormat> _audioFormatManager;

    private AFrameFormat _serverAudioFormat;

    private RTICallAudio _audio;

    private AudioResampler? _sourceResampler = null;

    private AudioResampler? _sinkResampler = null;

    private bool _helloResponseReceived;

    private int _responseAudioLength;

    protected bool _isAudioSourceStarted;

    protected bool _isAudioSinkStarted;

    protected bool _isAudioSourcePaused;

    protected bool _isAudioSinkPaused;

    protected bool _isAudioSourceClosed;

    protected bool _isAudioSinkClosed;

    [Obsolete("The audio source only generates encoded samples.")]
    public event RawAudioSampleDelegate OnAudioSourceRawSample
    {
        add
        {
        }
        remove
        {
        }
    }

    public event SourceErrorDelegate? OnAudioSourceError;

    public event SourceErrorDelegate? OnAudioSinkError;

    public RTICAudioEndPoint()
    {
        _logger = LogFactory.CreateLogger<RTICAudioEndPoint>();
        _info = new(_logger);

        OnAudioSourceEncodedFrameReady += AudioSourceEncodedFrameReady; // Throws not implemented.

        // 'Hello there' sample is enqueued into audio input stream when session starts.
        // It is a free sample from https://pixabay.com/sound-effects/quothello-therequot-158832/
        var helloSample = Resource1.hello_there;

        _audio = new RTICallAudio(_info, ConversationSessionConfig.AudioFormat);
        _audio.Start(null, helloSample);
        _helloResponseReceived = false;

        _audioEncoder = new AudioEncoder(true, true);
        var encoderSupported = _audioEncoder.SupportedFormats;
        _audioFormatManager = new MediaFormatManager<AudioFormat>(encoderSupported);

        _responseTimer = new();
        _responseTimer.Interval = RESPONSE_AUDIO_INTERVAL;
        _responseTimer.Elapsed += OnAudioResponseTimer;
        _responseTimer.AutoReset = true;

        _serverAudioFormat = ConversationSessionConfig.AudioFormat;
        _responseAudioLength = (int)_serverAudioFormat.BufferSizeFromMiliseconds(RESPONSE_AUDIO_INTERVAL);
    }

    public void Dispose()
    {
        _audio.Dispose();
        _sourceResampler?.Dispose();
        _sinkResampler?.Dispose();
    }

    public void ConnectToConversation(EventCollection conversationEvents, RTICallConsole callConsole)
    {
        conversationEvents.Connect<ConversationResponseStarted>(HandleEvent);
        conversationEvents.Connect<ConversationResponseFinished>(HandleEvent);
        conversationEvents.Connect<ConversationInputSpeechStarted>(HandleEvent);
        conversationEvents.Connect<ConversationInputSpeechFinished>(HandleEvent);
        conversationEvents.Connect<ConversationInputTranscriptionFinished>(HandleEvent);
        conversationEvents.Connect<ConversationInputTranscriptionFailed>(HandleEvent);
        conversationEvents.Connect<ConversationItemStreamingPartDelta>(HandleEvent);
        _audio.ConnectToConversation(conversationEvents, callConsole);
    }

    public void RestrictFormats(Func<AudioFormat, bool> filter)
        => throw new NotImplementedException();

    public List<AudioFormat> GetAudioSourceFormats()
    {
        return _audioFormatManager.GetSourceFormats();
    }

    public List<AudioFormat> GetAudioSinkFormats()
        => throw new NotImplementedException();

    public bool HasEncodedAudioSubscribers()
    {
        return this.OnAudioSourceEncodedSample != null;
    }

    public bool IsAudioSourcePaused()
    {
        return _isAudioSourcePaused;
    }

    public bool IsAudioSinkPaused()
    {
        return _isAudioSinkPaused;
    }

    public void ExternalAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample) 
        => throw new NotImplementedException();

    public void SetAudioSourceFormat(AudioFormat audioFormat)
    {
        _sourceResampler = AudioResampler.Create(new()
        {
            Input = new AFrameFormat(ASampleFormat.S16, audioFormat.ClockRate, 1),
            Output = new AFrameFormat(ASampleFormat.S16, 24000, 1),
        });
        _sinkResampler = AudioResampler.Create(new()
        {
            Input = new AFrameFormat(ASampleFormat.S16, 24000, 1),
            Output = new AFrameFormat(ASampleFormat.S16, audioFormat.ClockRate, 1),
        });
        _audioFormatManager.SetSelectedFormat(audioFormat);
    }

    public void SetAudioSinkFormat(AudioFormat audioFormat)
        => throw new NotImplementedException();

    public MediaEndPoints ToMediaEndPoints()
    {
        return new MediaEndPoints
        {
            AudioSource = this,
            AudioSink = this
        };
    }

    public Task StartAudio()
    {
        if (!_isAudioSourceStarted)
        {
            _isAudioSourceStarted = true;
        }

        return Task.CompletedTask;
    }

    public Task CloseAudio()
    {
        if (!_isAudioSourceClosed)
        {
            _isAudioSourceClosed = true;
            _responseTimer.Stop();
        }

        return Task.CompletedTask;
    }

    public Task PauseAudio()
    {
        _isAudioSourcePaused = true;
        return Task.CompletedTask;
    }

    public Task ResumeAudio()
    {
        _isAudioSourcePaused = false;
        return Task.CompletedTask;
    }

    public static byte[] Combine(byte[] first, byte[] second)
    {
        byte[] ret = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, ret, 0, first.Length);
        Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
        return ret;
    }

    private void AudioSourceEncodedFrameReady(EncodedAudioFrame frame) => throw new NotImplementedException();

    /// <summary>
    /// The thing with the response in SIP call session is to keep sending audio in real time,
    /// it cannot be simply enqued all at once as was received from WebSocket realtime API.
    /// <para>Then there is also an issue of occasional small chunks at the begining of response.
    /// All this is hopefully reasonably solved here.</para>
    /// <para>All this is going on even while audio source is paused. Not sure if this is right way to do it.</para>
    /// </summary>
    /// <param name="source"></param>
    /// <param name="e"></param>
    protected void OnAudioResponseTimer(Object? source, System.Timers.ElapsedEventArgs e)
    {
        if (Interlocked.CompareExchange(ref _timerLock, 1, 0) == 0)
        {
            var speaker = _audio.SpeakerOutput;
            if (_sinkResampler is not null)
            {
                byte[]? resultArray = null;

                int retryWatchdog = 0;
                int totalSamplesRead = 0;

                // Trying to process exactly 'RESPONSE_AUDIO_INTERVAL' of bytes from source buffer each time.
                while (totalSamplesRead < _responseAudioLength)
                {
                    if (speaker.StoredByteCount > 0)
                    {
                        short[] buffer = new short[_responseAudioLength];
                        int samplesRead = speaker.Read(buffer, 0, buffer.Length - totalSamplesRead);
                        if (samplesRead > 0)
                        {
                            if (!_isAudioSourcePaused)
                            {
                                short[] outAudio = _sinkResampler.Process(buffer, 0, samplesRead);
                                if (outAudio.Length > 0)
                                {
                                    byte[] array = _audioEncoder.EncodeAudio(outAudio, _audioFormatManager.SelectedFormat);
                                    if (resultArray is not null)
                                    {
                                        resultArray = Combine(resultArray, array);
                                    }
                                    else
                                    {
                                        resultArray = array;
                                    }
                                }
                            }
                            totalSamplesRead += samplesRead;
                        }
                    }
                    else
                    {
                        // This part should be useful at the begining of the response,
                        // in cases when response arrives in small chunks of audio.
                        if (retryWatchdog < 3)
                        {
                            retryWatchdog++;
                            Thread.Sleep(25);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (!_isAudioSourcePaused && resultArray is not null)
                {
                    this.OnAudioSourceEncodedSample?.Invoke((uint)resultArray.Length, resultArray);
                }
            }

            _timerLock = 0;
        }
        else
        {
            _logger.LogDebug("(RTICAudioEndPoint) OnAudioResponseTimer stuck.");
        }
    }

    public void GotAudioRtp(IPEndPoint remoteEndPoint, uint ssrc, uint seqnum, uint timestamp, int payloadID, bool marker, byte[] payload)
        => throw new NotImplementedException();

    void IAudioSink.GotEncodedMediaFrame(EncodedAudioFrame encodedMediaFrame) 
    {
        var microphone = _audio.MicrophoneInput;
        if (!_isAudioSinkPaused && _audioEncoder != null && _sourceResampler is not null)
        {
            if (Interlocked.CompareExchange(ref _encodedMediaLock, 1, 0) == 0)
            {
                short[] inAudio = _audioEncoder.DecodeAudio(encodedMediaFrame.EncodedAudio, _audioFormatManager.SelectedFormat);
                var outAudio = _sourceResampler.Process(inAudio);
                if (outAudio.Length > 0)
                {
                    microphone.Write(outAudio, 0, outAudio.Length);
                }

                _encodedMediaLock = 0;
            }
            else
            {
                _logger.LogDebug("(RTICAudioEndPoint) GotEncodedMediaFrame stuck.");
            }
        }
    }

    public Task PauseAudioSink()
    {
        _isAudioSinkPaused = true;
        return Task.CompletedTask;
    }

    public Task ResumeAudioSink()
    {
        _isAudioSinkPaused = false;
        return Task.CompletedTask;
    }

    public Task StartAudioSink()
    {
        if (!_isAudioSinkStarted)
        {
            _isAudioSinkStarted = true;
            _responseTimer.Start();
        }

        return Task.CompletedTask;
    }

    public Task CloseAudioSink()
    {
        if (!_isAudioSinkClosed)
        {
            _isAudioSinkClosed = true;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Response started.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private void HandleEvent(object? s, ConversationResponseStarted update)
    {
        LogDebug("(RTICAudioEndPoint) Item started.");
    }

    /// <summary>
    /// Response finished.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private void HandleEvent(object? s, ConversationResponseFinished update)
    {
        LogDebug("(RTICAudioEndPoint) Item finished.");
    }

    private void HandleEvent(object? s, ConversationInputSpeechStarted update)
    {
        LogDebug("(RTICAudioEndPoint) Speech started.");
    }

    private void HandleEvent(object? s, ConversationInputSpeechFinished update)
    {
        LogDebug("(RTICAudioEndPoint) Speech finished.");
    }

    /// <summary>
    /// Complete transcription of user's speech.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private void HandleEvent(object? s, ConversationInputTranscriptionFinished update)
    {
        if (!String.IsNullOrEmpty(update.Transcript))
        {
            LogDebug("(RTICAudioEndPoint) Input transcription finished.");
        }
    }

    /// <summary>
    /// Transcription of user's speech has failed.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private void HandleEvent(object? s, ConversationInputTranscriptionFailed update)
    {
        if (!String.IsNullOrEmpty(update.ErrorMessage))
        {
            LogDebug("(RTICAudioEndPoint) Input transcription failed: " + update.ErrorMessage);
        }
        else
        {
            LogDebug("(RTICAudioEndPoint) Input transcription failed.");
        }
    }

    /// <summary>
    /// This update brings text and audio from AI agent.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="update"></param>
    private void HandleEvent(object? s, ConversationItemStreamingPartDelta update)
    {
        if (update.Audio is not null)
        {
            var speaker = _audio.Speaker;
            if (!_isAudioSourceClosed && !_isAudioSourcePaused)
            {
                if (!_helloResponseReceived)
                {
                    _helloResponseReceived = true;
                    LogDebug("(RTICAudioEndPoint) Total bytes buffered: " + _audio.SpeakerOutput.StoredByteCount);
                }

                var buffer = update.Audio.ToArray();
                speaker.Write(buffer, 0, buffer.Length);
            }
            else if (!_helloResponseReceived)
            {
                var buffer = update.Audio.ToArray();
                speaker?.Write(buffer, 0, buffer.Length);
                LogDebug("(RTICAudioEndPoint) Size added to buffer: " + buffer.Length);
            }
        }
    }

    private void LogDebug(string message)
    {
#if DEBUG
        _logger.LogDebug(message);
#endif
    }
}
