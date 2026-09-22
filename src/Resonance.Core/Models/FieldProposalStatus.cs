namespace Resonance.Core.Models;

/// <summary>
///     Represents the comparison outcome between the local tag and the proposed remote value.
/// </summary>
public enum FieldProposalStatus
{
    /// <summary>The local value and the remote value are semantically identical.</summary>
    Unchanged = 0,

    /// <summary>The local value was empty/null and the provider supplied a new value.</summary>
    NewValue = 1,

    /// <summary>The remote value proposes an update or casing/formatting refinement to the existing local value.</summary>
    Updated = 2,

    /// <summary>A significant divergence exists between the local value and the remote value (e.g., completely different album/title).</summary>
    Conflict = 3
}
