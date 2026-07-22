using AudioFormatLib;
using OpenAI.Realtime;

namespace LibRTIC.Realtime;

#pragma warning disable OPENAI002

/// <summary>Fixed PCM media contract shared by every LibRTIC realtime conversation.</summary>
public static class RealtimeAudioContract
{
    public const ASampleValueFormat SampleValueFormat = ASampleValueFormat.S16;
    public const int SamplesPerSecond = 24000;
    public const int ChannelCount = 1;
    public const int InputBufferSeconds = 2;
    public static readonly APcmFormat AudioFormat = new(
        SampleValueFormat,
        SamplesPerSecond,
        ChannelCount,
        byteOrder: AByteOrder.LittleEndian);
}

/// <summary>Fixed session behavior shared by every LibRTIC realtime conversation.</summary>
public static class RealtimeSessionDefaults
{
    public const string InputTranscriptionModel = "whisper-1";
    public static readonly RealtimeVoice OutputVoice = RealtimeVoice.Alloy;
    public const bool CreateResponseEnabled = true;
    public const bool InterruptResponseEnabled = false;
}
