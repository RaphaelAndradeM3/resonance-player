using CommunityToolkit.Mvvm.ComponentModel;

namespace Resonance.Core.Models;

/// <summary>
///     Representa o registro individual de comparação e seleção de um campo de metadados.
/// </summary>
public partial class TagDiffRecord : ObservableObject
{
    /// <summary>Gets the canonical internal key of the field (e.g., "Title", "Artist", "Year", "CoverArt").</summary>
    public string FieldKey { get; init; } = string.Empty;

    /// <summary>Gets the human-readable localized display name of the field (e.g., "Título", "Artista").</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets the current local value in the physical file or database, or null if unset.</summary>
    public string? OriginalValue { get; init; }

    /// <summary>Gets the proposed new value, or null if unset.</summary>
    public string? ProposedValue { get; init; }

    /// <summary>Gets the status classification comparing original and proposed values.</summary>
    public FieldProposalStatus Status { get; init; }

    /// <summary>Gets the provenance of the proposed value.</summary>
    public MetadataProvenance Provenance { get; init; }

    /// <summary>Gets whether this diff record represents cover art/picture data rather than plain text/numbers.</summary>
    public bool IsPictureField { get; init; }

    /// <summary>Gets or sets whether this field change is selected by the user to be written to disk.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Gets whether this record indicates an actual change between original and proposed.</summary>
    public bool HasChanged => Status != FieldProposalStatus.Unchanged;

    /// <summary>Gets the localized display label for the status badge.</summary>
    public string StatusDisplay => Status switch
    {
        FieldProposalStatus.NewValue => "Novo",
        FieldProposalStatus.Updated => "Atualizado",
        FieldProposalStatus.Conflict => "Conflito",
        _ => "Inalterado"
    };

    /// <summary>Gets the display label for the provenance badge.</summary>
    public string ProvenanceDisplay => Provenance switch
    {
        MetadataProvenance.MusicBrainz => "MusicBrainz",
        MetadataProvenance.CoverArtArchive => "Cover Art Archive",
        MetadataProvenance.UserOverride => "Manual",
        _ => "Local"
    };
}
