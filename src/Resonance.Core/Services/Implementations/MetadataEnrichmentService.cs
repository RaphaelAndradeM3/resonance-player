using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;

namespace Resonance.Core.Services.Implementations;

/// <summary>
///     Pure computational service responsible for comparing local track metadata with
///     remote online suggestions and generating comprehensive <see cref="EnrichmentProposal"/>
///     records with field-level provenance, status, and interactive selection.
/// </summary>
public class MetadataEnrichmentService : IMetadataEnrichmentService
{
    private readonly ILogger<MetadataEnrichmentService> _logger;

    public MetadataEnrichmentService(ILogger<MetadataEnrichmentService>? logger = null)
    {
        _logger = logger ?? NullLogger<MetadataEnrichmentService>.Instance;
    }

    /// <inheritdoc />
    public EnrichmentProposal CreateProposal(
        string filePath,
        TrackTagDetails localTagDetails,
        MusicBrainzRecordingDetail remoteData,
        string? coverArtUrl = null,
        Guid? songId = null,
        string? originalCoverPath = null)
    {
        var localTags = TrackAudioTags.FromTrackTagDetails(localTagDetails);
        return CreateProposal(filePath, localTags, remoteData, coverArtUrl, songId, originalCoverPath);
    }

    /// <inheritdoc />
    public EnrichmentProposal CreateProposal(
        string filePath,
        TrackAudioTags localTags,
        MusicBrainzRecordingDetail remoteData,
        string? coverArtUrl = null,
        Guid? songId = null,
        string? originalCoverPath = null)
    {
        ArgumentNullException.ThrowIfNull(localTags);
        ArgumentNullException.ThrowIfNull(remoteData);

        var proposals = new List<FieldProposal>();

        // 1. Title
        proposals.Add(CompareStringField(
            "Título",
            "Title",
            localTags.Title,
            remoteData.Title,
            MetadataProvenance.MusicBrainz));

        // 2. Artist
        proposals.Add(CompareStringField(
            "Artista",
            "Artist",
            localTags.Artist,
            remoteData.Artist,
            MetadataProvenance.MusicBrainz));

        // 3. Album
        proposals.Add(CompareStringField(
            "Álbum",
            "Album",
            localTags.Album,
            remoteData.Album,
            MetadataProvenance.MusicBrainz));

        // 4. Album Artist
        proposals.Add(CompareStringField(
            "Artista do Álbum",
            "AlbumArtist",
            localTags.AlbumArtist,
            remoteData.AlbumArtist,
            MetadataProvenance.MusicBrainz));

        // 5. Year
        proposals.Add(CompareIntField(
            "Ano",
            "Year",
            localTags.Year,
            remoteData.Year,
            MetadataProvenance.MusicBrainz,
            treatDifferenceAsConflict: true));

        // 6. Track Number
        proposals.Add(CompareIntField(
            "Número da Faixa",
            "TrackNumber",
            localTags.TrackNumber,
            remoteData.TrackNumber,
            MetadataProvenance.MusicBrainz));

        // 7. Total Tracks
        proposals.Add(CompareIntField(
            "Total de Faixas",
            "TotalTracks",
            localTags.TotalTracks,
            remoteData.TotalTracks,
            MetadataProvenance.MusicBrainz));

        // 8. Disc Number
        proposals.Add(CompareIntField(
            "Número do Disco",
            "DiscNumber",
            localTags.DiscNumber,
            remoteData.DiscNumber,
            MetadataProvenance.MusicBrainz));

        // 9. Total Discs
        proposals.Add(CompareIntField(
            "Total de Discos",
            "TotalDiscs",
            localTags.TotalDiscs,
            remoteData.TotalDiscs,
            MetadataProvenance.MusicBrainz));

        // 10. Genre (Semantic merge per FR-013)
        var mergedGenre = MergeGenres(localTags.Genre, remoteData.Genres);
        proposals.Add(CompareGenreField(localTags.Genre, mergedGenre));

        // 11. Label
        proposals.Add(CompareStringField(
            "Gravadora",
            "Label",
            localTags.Label,
            remoteData.Label,
            MetadataProvenance.MusicBrainz));

        // 12. ISRC
        proposals.Add(CompareStringField(
            "ISRC",
            "Isrc",
            localTags.Isrc,
            remoteData.Isrc,
            MetadataProvenance.MusicBrainz));

        // 13. Cover Art
        proposals.Add(CompareCoverArt(originalCoverPath, coverArtUrl));

        // Confidence score between 0.0 and 1.0 based on provider match score
        var score = remoteData.Score > 0 ? Math.Clamp(remoteData.Score / 100.0, 0.0, 1.0) : 0.8;

        // Derive 250px thumbnail URL from 500px URL if applicable
        string? thumbnailUrl = null;
        if (!string.IsNullOrEmpty(coverArtUrl))
        {
            thumbnailUrl = coverArtUrl.EndsWith("-500")
                ? coverArtUrl[..^4] + "-250"
                : coverArtUrl;
        }

        return new EnrichmentProposal
        {
            SongId = songId,
            FilePath = filePath,
            RecordingMbid = remoteData.RecordingId,
            ReleaseMbid = remoteData.ReleaseId,
            Proposals = proposals.AsReadOnly(),
            OriginalCoverPath = originalCoverPath,
            ProposedCoverUrl = coverArtUrl,
            ProposedCoverThumbnailUrl = thumbnailUrl,
            ConfidenceScore = score,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <inheritdoc />
    public string MergeGenres(string? localGenre, IReadOnlyList<string>? remoteGenres)
    {
        var resultList = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Process local genres first to preserve local priority and casing
        if (!string.IsNullOrWhiteSpace(localGenre))
        {
            var localParts = localGenre.Split(new[] { ';', ',', '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in localParts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0 && seen.Add(trimmed))
                {
                    resultList.Add(trimmed);
                }
            }
        }

        // 2. Append remote MusicBrainz tags if not already seen
        if (remoteGenres != null)
        {
            foreach (var remote in remoteGenres)
            {
                if (string.IsNullOrWhiteSpace(remote))
                    continue;

                var trimmed = remote.Trim();
                if (trimmed.Length > 0 && seen.Add(trimmed))
                {
                    resultList.Add(trimmed);
                }
            }
        }

        return string.Join("; ", resultList);
    }

    #region Field Comparison Helpers

    private static FieldProposal CompareStringField(
        string fieldName,
        string fieldKey,
        string? currentValue,
        string? proposedValue,
        MetadataProvenance provenance)
    {
        var currentClean = currentValue?.Trim();
        var proposedClean = proposedValue?.Trim();

        // Both empty
        if (string.IsNullOrEmpty(currentClean) && string.IsNullOrEmpty(proposedClean))
        {
            return new FieldProposal
            {
                FieldName = fieldName,
                FieldKey = fieldKey,
                CurrentValue = null,
                ProposedValue = null,
                Status = FieldProposalStatus.Unchanged,
                Provenance = MetadataProvenance.LocalTag,
                IsSelected = false
            };
        }

        // Local empty, remote provided -> NewValue
        if (string.IsNullOrEmpty(currentClean) && !string.IsNullOrEmpty(proposedClean))
        {
            return new FieldProposal
            {
                FieldName = fieldName,
                FieldKey = fieldKey,
                CurrentValue = null,
                ProposedValue = proposedClean,
                Status = FieldProposalStatus.NewValue,
                Provenance = provenance,
                IsSelected = true
            };
        }

        // Local has value, remote empty -> Unchanged
        if (!string.IsNullOrEmpty(currentClean) && string.IsNullOrEmpty(proposedClean))
        {
            return new FieldProposal
            {
                FieldName = fieldName,
                FieldKey = fieldKey,
                CurrentValue = currentClean,
                ProposedValue = currentClean,
                Status = FieldProposalStatus.Unchanged,
                Provenance = MetadataProvenance.LocalTag,
                IsSelected = false
            };
        }

        // Both non-empty: exact match
        if (string.Equals(currentClean, proposedClean, StringComparison.Ordinal))
        {
            return new FieldProposal
            {
                FieldName = fieldName,
                FieldKey = fieldKey,
                CurrentValue = currentClean,
                ProposedValue = proposedClean,
                Status = FieldProposalStatus.Unchanged,
                Provenance = MetadataProvenance.LocalTag,
                IsSelected = false
            };
        }

        // Case-insensitive match or whitespace refinement -> Updated
        if (string.Equals(currentClean, proposedClean, StringComparison.OrdinalIgnoreCase))
        {
            return new FieldProposal
            {
                FieldName = fieldName,
                FieldKey = fieldKey,
                CurrentValue = currentClean,
                ProposedValue = proposedClean,
                Status = FieldProposalStatus.Updated,
                Provenance = provenance,
                IsSelected = true
            };
        }

        // Substring / variation match (e.g. "Radiohead (Remastered)" vs "Radiohead") -> Updated
        if (IsSubstringOrVariation(currentClean!, proposedClean!))
        {
            return new FieldProposal
            {
                FieldName = fieldName,
                FieldKey = fieldKey,
                CurrentValue = currentClean,
                ProposedValue = proposedClean,
                Status = FieldProposalStatus.Updated,
                Provenance = provenance,
                IsSelected = true
            };
        }

        // Significant divergence -> Conflict (defaults to unchecked per FR-012)
        return new FieldProposal
        {
            FieldName = fieldName,
            FieldKey = fieldKey,
            CurrentValue = currentClean,
            ProposedValue = proposedClean,
            Status = FieldProposalStatus.Conflict,
            Provenance = provenance,
            IsSelected = false
        };
    }

    private static FieldProposal CompareIntField(
        string fieldName,
        string fieldKey,
        int? currentValue,
        int? proposedValue,
        MetadataProvenance provenance,
        bool treatDifferenceAsConflict = false)
    {
        var curValid = currentValue.HasValue && currentValue.Value > 0;
        var propValid = proposedValue.HasValue && proposedValue.Value > 0;

        if (!curValid && !propValid)
        {
            return new FieldProposal
            {
                FieldName = fieldName,
                FieldKey = fieldKey,
                CurrentValue = null,
                ProposedValue = null,
                Status = FieldProposalStatus.Unchanged,
                Provenance = MetadataProvenance.LocalTag,
                IsSelected = false
            };
        }

        if (!curValid && propValid)
        {
            return new FieldProposal
            {
                FieldName = fieldName,
                FieldKey = fieldKey,
                CurrentValue = null,
                ProposedValue = proposedValue!.Value.ToString(),
                Status = FieldProposalStatus.NewValue,
                Provenance = provenance,
                IsSelected = true
            };
        }

        if (curValid && !propValid)
        {
            return new FieldProposal
            {
                FieldName = fieldName,
                FieldKey = fieldKey,
                CurrentValue = currentValue!.Value.ToString(),
                ProposedValue = currentValue.Value.ToString(),
                Status = FieldProposalStatus.Unchanged,
                Provenance = MetadataProvenance.LocalTag,
                IsSelected = false
            };
        }

        if (currentValue!.Value == proposedValue!.Value)
        {
            return new FieldProposal
            {
                FieldName = fieldName,
                FieldKey = fieldKey,
                CurrentValue = currentValue.Value.ToString(),
                ProposedValue = proposedValue.Value.ToString(),
                Status = FieldProposalStatus.Unchanged,
                Provenance = MetadataProvenance.LocalTag,
                IsSelected = false
            };
        }

        var status = treatDifferenceAsConflict
            ? FieldProposalStatus.Conflict
            : FieldProposalStatus.Updated;

        return new FieldProposal
        {
            FieldName = fieldName,
            FieldKey = fieldKey,
            CurrentValue = currentValue.Value.ToString(),
            ProposedValue = proposedValue.Value.ToString(),
            Status = status,
            Provenance = provenance,
            IsSelected = status != FieldProposalStatus.Conflict
        };
    }

    private static FieldProposal CompareGenreField(string? currentGenre, string mergedGenre)
    {
        var curClean = currentGenre?.Trim();
        var mergedClean = mergedGenre.Trim();

        if (string.IsNullOrEmpty(curClean) && string.IsNullOrEmpty(mergedClean))
        {
            return new FieldProposal
            {
                FieldName = "Gênero",
                FieldKey = "Genre",
                CurrentValue = null,
                ProposedValue = null,
                Status = FieldProposalStatus.Unchanged,
                Provenance = MetadataProvenance.LocalTag,
                IsSelected = false
            };
        }

        if (string.IsNullOrEmpty(curClean) && !string.IsNullOrEmpty(mergedClean))
        {
            return new FieldProposal
            {
                FieldName = "Gênero",
                FieldKey = "Genre",
                CurrentValue = null,
                ProposedValue = mergedClean,
                Status = FieldProposalStatus.NewValue,
                Provenance = MetadataProvenance.MusicBrainz,
                IsSelected = true
            };
        }

        if (string.Equals(curClean, mergedClean, StringComparison.OrdinalIgnoreCase))
        {
            return new FieldProposal
            {
                FieldName = "Gênero",
                FieldKey = "Genre",
                CurrentValue = curClean,
                ProposedValue = mergedClean,
                Status = FieldProposalStatus.Unchanged,
                Provenance = MetadataProvenance.LocalTag,
                IsSelected = false
            };
        }

        // New genres added to existing genre -> Updated with IsSelected = true
        return new FieldProposal
        {
            FieldName = "Gênero",
            FieldKey = "Genre",
            CurrentValue = curClean,
            ProposedValue = mergedClean,
            Status = FieldProposalStatus.Updated,
            Provenance = MetadataProvenance.MusicBrainz,
            IsSelected = true
        };
    }

    private static FieldProposal CompareCoverArt(string? originalCoverPath, string? proposedCoverUrl)
    {
        var hasOriginal = !string.IsNullOrWhiteSpace(originalCoverPath);
        var hasProposed = !string.IsNullOrWhiteSpace(proposedCoverUrl);

        if (!hasProposed)
        {
            return new FieldProposal
            {
                FieldName = "Arte de Capa",
                FieldKey = "CoverArt",
                CurrentValue = originalCoverPath,
                ProposedValue = originalCoverPath,
                Status = FieldProposalStatus.Unchanged,
                Provenance = hasOriginal ? MetadataProvenance.LocalTag : MetadataProvenance.LocalTag,
                IsSelected = false
            };
        }

        if (!hasOriginal)
        {
            return new FieldProposal
            {
                FieldName = "Arte de Capa",
                FieldKey = "CoverArt",
                CurrentValue = null,
                ProposedValue = proposedCoverUrl,
                Status = FieldProposalStatus.NewValue,
                Provenance = MetadataProvenance.CoverArtArchive,
                IsSelected = true
            };
        }

        // Existing local cover + new online cover available -> Updated
        return new FieldProposal
        {
            FieldName = "Arte de Capa",
            FieldKey = "CoverArt",
            CurrentValue = originalCoverPath,
            ProposedValue = proposedCoverUrl,
            Status = FieldProposalStatus.Updated,
            Provenance = MetadataProvenance.CoverArtArchive,
            IsSelected = true
        };
    }

    private static bool IsSubstringOrVariation(string a, string b)
    {
        // Normalize common punctuation and spacing
        var cleanA = Regex.Replace(a, @"[^\w\s]", "").Trim();
        var cleanB = Regex.Replace(b, @"[^\w\s]", "").Trim();

        if (string.IsNullOrEmpty(cleanA) || string.IsNullOrEmpty(cleanB))
            return false;

        return cleanA.Contains(cleanB, StringComparison.OrdinalIgnoreCase) ||
               cleanB.Contains(cleanA, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
