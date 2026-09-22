namespace Resonance.Core.Models;

/// <summary>
///     Encapsulates a proposed change for an individual metadata field,
///     supporting interactive selection per FR-012.
/// </summary>
public class FieldProposal
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
    public bool IsSelected { get; set; }
}
