using LibRTIC.BasicDevices.RTIC;
using LibRTIC.MiniTaskLib.Model;
using Timer = System.Timers.Timer;

namespace LibRTIC.BasicDevices;

public abstract class RTIConsoleAudio : IDisposable
{
    private const int INPUT_AUDIO_ACTION_PERIOD = 500;

    public abstract ExStream? Speaker { get; }

    public abstract ExStream? Microphone { get; }

    public virtual float Volume { get; set; }

    protected Info _info;

    protected AudioStreamFormat _audioFormat;

    protected CancellationToken _cancellation;

    protected RTIConsoleStateId _state = RTIConsoleStateId.Inactive;

    protected Timer _timer;

    protected bool _firstResponseReceived = false;

    protected byte[]? _helloSample = null;

    private byte[] _inpuAudio = new byte[1024 * 16];


    public RTIConsoleAudio(Info info,
                           AudioStreamFormat audioFormat,
                           CancellationToken cancellation)
    {
        this._info = info;
        this._audioFormat = audioFormat;
        this._cancellation = cancellation;

        _timer = new();
        _timer.Interval = INPUT_AUDIO_ACTION_PERIOD;
        _timer.Elapsed += OnTimer;
        _timer.AutoReset = true;
    }

    public abstract void Dispose();

    public abstract void Start(byte[]? waitingMusic = null, byte[]? helloSample = null);

    public virtual void HandleEvent(object? sender, RTIConsoleStateId state)
    {
        _state = state;
        if (_state == RTIConsoleStateId.Answering && !_firstResponseReceived)
        {
            if (_helloSample is not null && Microphone is not null)
            {
                Microphone.ClearBuffer();
                Microphone.Write(_helloSample, 0, _helloSample.Length);
                _timer.Start();
            }
        }
        else if (_state == RTIConsoleStateId.WritingItem && !_firstResponseReceived)
        {
            _firstResponseReceived = true;
        }
    }

    protected void OnTimer(Object? source, System.Timers.ElapsedEventArgs e)
    {
        if (!_firstResponseReceived && _inpuAudio is not null && Microphone is not null)
        {
            Microphone.Write(_inpuAudio, 0, _inpuAudio.Length);
        }
        else
        {
            _timer.Stop();
        }
    }
}
