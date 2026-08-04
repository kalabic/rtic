using AudioFormatLib;
using NAudio.Wave;

namespace LibRTIC_Win.BasicDevices;

/// <summary>
/// Validates the interleaved little-endian S16 contract shared with NAudio devices.
/// </summary>
internal static class NAudioS16Format
{
    internal static WaveFormat CreateWaveFormat(
        ASampleFormat format,
        string? paramName = null)
    {
        if (format.ValueFormat != AValueFormat.S16)
        {
            throw new ArgumentException(
                "NAudio device streams require signed 16-bit PCM samples.",
                paramName ?? nameof(format));
        }
        if (format.SampleRate <= 0 || format.ChannelCount <= 0)
        {
            throw new ArgumentException(
                "NAudio device streams require a positive sample rate and channel count.",
                paramName ?? nameof(format));
        }
        if (format.ByteOrder.Resolve() != AByteOrder.LittleEndian)
        {
            throw new ArgumentException(
                "NAudio device streams require little-endian PCM samples.",
                paramName ?? nameof(format));
        }

        return new WaveFormat(
            format.SampleRate,
            format.ValueFormat.Bits(),
            format.ChannelCount);
    }
}
