using CommunityToolkit.Mvvm.ComponentModel;

namespace Resonance.Core.Models;

/// <summary>
///     Encapsulates a proposed change for an individual metadata field,
///     supporting interactive selection per FR-012.
/// </summary>
public partial class FieldProposal : ObservableObject
{
    /// <summary>Gets the display name of the field (e.g. "Título", "Artista", "Álbum", "Ano").</summary>
    public string FieldName { get; init; } = string.Empty;

    /// <summary>Gets the canonical internal key of the field (e.g. "Title", "Artist", "Year").</summary>
    public string FieldKey { get; init; } = string.Empty;

    /// <summary>Gets the current local value in the file/library, or null if unset.</summary>
    public string? CurrentValue { get; init; }

    /// <summary>Gets the value suggested by the remote provider, or null if unavailable.</summary>
    public string? ProposedValue { get; init; }

    /// <summary>Gets the comparison status for this field.</summary>
    public FieldProposalStatus Status { get; init; }

    /// <summary>Gets the origin of the proposed value.</summary>
    public MetadataProvenance Provenance { get; init; }

    /// <summary>Gets or sets whether this proposal is selected by the user to be carried over to staging/review.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

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

    /// <summary>Gets whether the proposal status is NewValue.</summary>
    public bool IsNewValue => Status == FieldProposalStatus.NewValue;

    /// <summary>Gets whether the proposal status is Updated.</summary>
    public bool IsUpdated => Status == FieldProposalStatus.Updated;

    /// <summary>Gets whether the proposal status is Conflict.</summary>
    public bool IsConflict => Status == FieldProposalStatus.Conflict;

    /// <summary>Gets whether the proposal status is Unchanged.</summary>
    public bool IsUnchanged => Status == FieldProposalStatus.Unchanged;
}

