namespace Resonance.Core.Models;

/// <summary>
///     Identifies the originating source of a metadata property.
/// </summary>
public enum MetadataProvenance
{
    /// <summary>Extracted directly from the local file's embedded audio tags.</summary>
    LocalTag = 0,

    /// <summary>Retrieved from the MusicBrainz open encyclopedia.</summary>
    MusicBrainz = 1,

    /// <summary>Retrieved from the Cover Art Archive community database.</summary>
    CoverArtArchive = 2,

    /// <summary>Manually supplied or overridden by the user.</summary>
    UserOverride = 3
}
