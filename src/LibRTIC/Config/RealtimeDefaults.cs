using AudioFormatLib;
using OpenAI.Realtime;

namespace LibRTIC.Realtime;

#pragma warning disable OPENAI002

/// <summary>Fixed PCM media contract shared by every LibRTIC realtime conversation.</summary>
public static class RealtimeAudioContract
{
    public const AValueFormat ValueFormat = AValueFormat.S16;
    public const int SamplesPerSecond = 24000;
    public const int ChannelCount = 1;
    public const int InputBufferSeconds = 2;
    public static readonly ASampleFormat AudioFormat = new(
        ValueFormat,
        SamplesPerSecond,
        ChannelCount,
        byteOrder: AByteOrder.LittleEndian);

    /// <summary>
    /// Creates a packet containing complete PCM samples in the realtime session format.
    /// </summary>
    public static AudioPacket CreatePacket(ReadOnlySpan<byte> pcmBytes)
    {
        int bytesPerSample = AudioFormat.BytesPerSample;
        if ((pcmBytes.Length % bytesPerSample) != 0)
        {
            throw new ArgumentException(
                "Realtime PCM audio must contain complete samples.",
                nameof(pcmBytes));
        }

        var packet = new AudioPacket(
            AudioFormat,
            pcmBytes.Length / bytesPerSample);
        packet.SetBytes(pcmBytes);
        return packet;
    }

    /// <summary>Returns whether a PCM format matches the fixed realtime session format.</summary>
    public static bool IsCompatible(ASampleFormat format)
        => format.ValueFormat == ValueFormat
            && format.SampleRate == SamplesPerSecond
            && format.ChannelCount == ChannelCount
            && format.ByteOrder == AByteOrder.LittleEndian;

    /// <summary>
    /// Rejects uninitialized packets and packets outside the fixed realtime session format.
    /// </summary>
    public static void ValidatePacket(
        in AudioPacket packet,
        string? paramName = null)
    {
        if (!packet.IsInitialized)
        {
            throw new ArgumentException(
                "The audio packet must be initialized.",
                paramName ?? nameof(packet));
        }
        if (!IsCompatible(packet.Format))
        {
            throw new ArgumentException(
                "The audio packet does not match the realtime session format.",
                paramName ?? nameof(packet));
        }
    }
}

/// <summary>Fixed session behavior shared by every LibRTIC realtime conversation.</summary>
public static class RealtimeSessionDefaults
{
    public const string InputTranscriptionModel = "whisper-1";
    internal static readonly RealtimeVoice OutputVoice = RealtimeVoice.Alloy;
    public const bool CreateResponseEnabled = true;
    public const bool InterruptResponseEnabled = false;
}
