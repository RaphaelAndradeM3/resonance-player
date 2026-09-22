namespace Resonance.Core.Models;

/// <summary>
///     Represents a complete, consolidated metadata enrichment proposal for a track,
///     holding suggestions in memory without any physical modification to disk files.
/// </summary>
public class EnrichmentProposal
{
    /// <summary>Gets the unique identifier of the song in the local library, if indexed.</summary>
    public Guid? SongId { get; init; }

    /// <summary>Gets the absolute physical file path of the audio file.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Gets the MusicBrainz Recording ID associated with this proposal.</summary>
    public string RecordingMbid { get; init; } = string.Empty;

    /// <summary>Gets the MusicBrainz Release ID (Album) associated with this proposal.</summary>
    public string? ReleaseMbid { get; init; }

    /// <summary>Gets the list of individual field proposals.</summary>
    public IReadOnlyList<FieldProposal> Proposals { get; init; } = Array.Empty<FieldProposal>();

    /// <summary>Gets the path to the local cover art file, if any.</summary>
    public string? OriginalCoverPath { get; init; }

    /// <summary>Gets the resolved Cover Art Archive URL for the official front cover (500px).</summary>
    public string? ProposedCoverUrl { get; init; }

    /// <summary>Gets the thumbnail URL for the official front cover (250px).</summary>
    public string? ProposedCoverThumbnailUrl { get; init; }

    /// <summary>Gets the overall confidence score (0.0 to 1.0) of the match.</summary>
    public double ConfidenceScore { get; init; }

    /// <summary>Gets the timestamp when this proposal was generated.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the number of fields currently selected by the user.</summary>
    public int SelectedCount => Proposals.Count(p => p.IsSelected);

    /// <summary>Gets whether there are any actionable changes (NewValue, Updated or Conflict).</summary>
    public bool HasActionableChanges => Proposals.Any(p => p.Status != FieldProposalStatus.Unchanged);
}
