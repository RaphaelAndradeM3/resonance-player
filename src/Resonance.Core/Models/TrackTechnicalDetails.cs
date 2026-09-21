namespace Resonance.Core.Models;

/// <summary>
///     Encapsulates technical audio stream and physical file properties of a track.
/// </summary>
public class TrackTechnicalDetails
{
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileSizeFormatted { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string ContainerFormat { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public int? BitrateKbps { get; set; }
    public string BitrateMode { get; set; } = string.Empty;
    public int? SampleRateHz { get; set; }
    public int? BitDepth { get; set; }
    public int? Channels { get; set; }
    public string ChannelsDescription { get; set; } = string.Empty;
    public DateTime? FileCreatedDate { get; set; }
    public DateTime? FileModifiedDate { get; set; }
    public bool IsAccessible { get; set; } = true;

    public string FormatSummary => !string.IsNullOrWhiteSpace(AudioCodec) && !AudioCodec.Equals(ContainerFormat, StringComparison.OrdinalIgnoreCase)
        ? $"{ContainerFormat} ({AudioCodec})"
        : ContainerFormat;

    public string SampleRateFormatted => SampleRateHz.HasValue ? $"{SampleRateHz.Value / 1000.0:0.#} kHz" : "—";

    public string BitDepthFormatted
    {
        get
        {
            if (BitDepth.HasValue && BitDepth.Value > 0)
                return $"{BitDepth.Value}-bit";
            if (AudioCodec.Contains("MP3", StringComparison.OrdinalIgnoreCase) ||
                AudioCodec.Contains("AAC", StringComparison.OrdinalIgnoreCase) ||
                AudioCodec.Contains("Opus", StringComparison.OrdinalIgnoreCase) ||
                AudioCodec.Contains("Ogg", StringComparison.OrdinalIgnoreCase) ||
                ContainerFormat.Contains("MPEG", StringComparison.OrdinalIgnoreCase))
                return "N/A (Lossy)";
            return "—";
        }
    }

    public string BitrateFormatted
    {
        get
        {
            if (!BitrateKbps.HasValue || BitrateKbps.Value <= 0) return "—";
            return !string.IsNullOrWhiteSpace(BitrateMode) && BitrateMode != "Desconhecido"
                ? $"{BitrateKbps.Value} kbps ({BitrateMode})"
                : $"{BitrateKbps.Value} kbps";
        }
    }

    public string DurationFormatted => Duration.TotalHours >= 1
        ? Duration.ToString(@"h\:mm\:ss")
        : Duration.ToString(@"m\:ss");
}
