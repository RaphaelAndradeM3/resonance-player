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
}
