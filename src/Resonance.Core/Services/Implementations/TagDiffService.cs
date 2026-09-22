using System.Globalization;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;

namespace Resonance.Core.Services.Implementations;

/// <summary>
///     Implements field-by-field diff generation and write plan construction.
/// </summary>
public class TagDiffService : ITagDiffService
{
    /// <inheritdoc />
    public IReadOnlyList<TagDiffRecord> GenerateDiff(
        string filePath,
        TrackAudioTags currentTags,
        EnrichmentProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(currentTags);
        ArgumentNullException.ThrowIfNull(proposal);

        var records = new List<TagDiffRecord>();

        // Map existing field proposals from the enrichment engine
        foreach (var p in proposal.Proposals)
        {
            var isPicture = p.FieldKey.Equals("CoverArt", StringComparison.OrdinalIgnoreCase) ||
                            p.FieldKey.Equals("Picture", StringComparison.OrdinalIgnoreCase);

            records.Add(new TagDiffRecord
            {
                FieldKey = p.FieldKey,
                DisplayName = p.FieldName,
                OriginalValue = p.CurrentValue,
                ProposedValue = p.ProposedValue,
                Status = p.Status,
                Provenance = p.Provenance,
                IsPictureField = isPicture,
                IsSelected = p.IsSelected
            });
        }

        // If cover art was proposed from Cover Art Archive and not already present in Proposals list
        if (!string.IsNullOrWhiteSpace(proposal.ProposedCoverUrl) &&
            !records.Any(r => r.IsPictureField))
        {
            var hasLocalCover = !string.IsNullOrWhiteSpace(proposal.OriginalCoverPath);
            records.Add(new TagDiffRecord
            {
                FieldKey = "CoverArt",
                DisplayName = "Capa do Álbum",
                OriginalValue = proposal.OriginalCoverPath,
                ProposedValue = proposal.ProposedCoverUrl,
                Status = hasLocalCover ? FieldProposalStatus.Updated : FieldProposalStatus.NewValue,
                Provenance = MetadataProvenance.CoverArtArchive,
                IsPictureField = true,
                IsSelected = true
            });
        }

        return records;
    }

    /// <inheritdoc />
    public IReadOnlyList<TagDiffRecord> GenerateDiff(
        string filePath,
        TrackAudioTags originalTags,
        EditableTagModel editedModel)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(originalTags);
        ArgumentNullException.ThrowIfNull(editedModel);

        var records = new List<TagDiffRecord>
        {
            CompareField("Title", "Título", originalTags.Title, editedModel.Title),
            CompareField("Artist", "Artista", originalTags.Artist, editedModel.Artist),
            CompareField("Album", "Álbum", originalTags.Album, editedModel.Album),
            CompareField("AlbumArtist", "Artista do Álbum", originalTags.AlbumArtist, editedModel.AlbumArtist),
            CompareField("Year", "Ano", originalTags.Year?.ToString(CultureInfo.InvariantCulture), editedModel.Year?.ToString(CultureInfo.InvariantCulture)),
            CompareField("TrackNumber", "Faixa", originalTags.TrackNumber?.ToString(CultureInfo.InvariantCulture), editedModel.TrackNumber?.ToString(CultureInfo.InvariantCulture)),
            CompareField("TrackTotal", "Total de Faixas", originalTags.TotalTracks?.ToString(CultureInfo.InvariantCulture), editedModel.TrackTotal?.ToString(CultureInfo.InvariantCulture)),
            CompareField("DiscNumber", "Disco", originalTags.DiscNumber?.ToString(CultureInfo.InvariantCulture), editedModel.DiscNumber?.ToString(CultureInfo.InvariantCulture)),
            CompareField("DiscTotal", "Total de Discos", originalTags.TotalDiscs?.ToString(CultureInfo.InvariantCulture), editedModel.DiscTotal?.ToString(CultureInfo.InvariantCulture)),
            CompareField("Genre", "Gênero", originalTags.Genre, editedModel.Genre),
            CompareField("Comment", "Comentário", originalTags.Comment, editedModel.Comment)
        };

        if (editedModel.PictureBytes != null && editedModel.PictureBytes.Length > 0)
        {
            records.Add(new TagDiffRecord
            {
                FieldKey = "CoverArt",
                DisplayName = "Capa do Álbum",
                OriginalValue = null,
                ProposedValue = $"{editedModel.PictureBytes.Length / 1024} KB ({editedModel.PictureMimeType ?? "image/jpeg"})",
                Status = FieldProposalStatus.Updated,
                Provenance = MetadataProvenance.UserOverride,
                IsPictureField = true,
                IsSelected = true
            });
        }

        return records;
    }

    /// <inheritdoc />
    public TagWritePlan CreateWritePlan(
        string filePath,
        IEnumerable<TagDiffRecord> diffRecords,
        byte[]? newPictureBytes = null,
        string? pictureMimeType = null,
        bool removePicture = false,
        Guid? songId = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(diffRecords);

        var selectedChanges = diffRecords
            .Where(r => r.IsSelected && r.HasChanged)
            .ToList();

        return new TagWritePlan
        {
            FilePath = filePath,
            SongId = songId,
            SelectedChanges = selectedChanges,
            NewPictureBytes = newPictureBytes,
            PictureMimeType = pictureMimeType,
            RemovePicture = removePicture
        };
    }

    private static TagDiffRecord CompareField(string key, string displayName, string? original, string? proposed)
    {
        var origTrim = string.IsNullOrWhiteSpace(original) ? null : original.Trim();
        var propTrim = string.IsNullOrWhiteSpace(proposed) ? null : proposed.Trim();

        FieldProposalStatus status;
        bool isSelected;

        if (string.Equals(origTrim, propTrim, StringComparison.Ordinal))
        {
            status = FieldProposalStatus.Unchanged;
            isSelected = false;
        }
        else if (origTrim == null && propTrim != null)
        {
            status = FieldProposalStatus.NewValue;
            isSelected = true;
        }
        else
        {
            status = FieldProposalStatus.Updated;
            isSelected = true;
        }

        return new TagDiffRecord
        {
            FieldKey = key,
            DisplayName = displayName,
            OriginalValue = origTrim,
            ProposedValue = propTrim,
            Status = status,
            Provenance = MetadataProvenance.UserOverride,
            IsPictureField = false,
            IsSelected = isSelected
        };
    }
}
