namespace LibRTIC.BasicDevices;

public abstract class ExStream : Stream
{
    public abstract bool IsClosed { get; }

    public abstract void Cancel();

    public abstract void ClearBuffer();

    public abstract int GetBytesAvailable();

    public abstract int GetBytesUnused();

    public abstract void SetBufferRequest(int value);

    public abstract int GetBufferRequest();

    /// <summary>
    /// Read packet of data exactly the size of provided buffer and write it into other provided stream.
    /// </summary>
    /// <param name="other"></param>
    /// <param name="buffer"></param>
    /// <returns></returns>
    public abstract int MovePacket(ExStream other, byte[] buffer);
}
