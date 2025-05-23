using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.BasicDevices;

public class AudioStreamBuffer : CircularBufferStream
{
    private AudioStreamFormat _format;

    private int _minBufferSize = 0;

    public AudioStreamBuffer(Info info, AudioStreamFormat audioFormat, int bufferSeconds, CancellationToken cancellation) 
        : base(info, audioFormat.BufferSizeFromSeconds(bufferSeconds), cancellation)
    {
        _format = audioFormat;
    }

    public void SetWaitMinimumData(int miliseconds)
    {
        _minBufferSize = _format.BufferSizeFromMiliseconds(miliseconds);
    }

    // TODO: Make async version
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_minBufferSize > 0)
        {
            int available = GetBytesAvailable(0);
            int minAsked = (_minBufferSize < count) ? _minBufferSize : count;
            if (available < minAsked)
            {
                if (!WaitDataAvailable(minAsked))
                {
                    return 0;
                }
            }
        }

        return base.Read(buffer, offset, count);
    }
}
