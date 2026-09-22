# Contract: `IMusicBrainzService`

**Namespace**: `Resonance.Core.Services.Abstractions`  
**Implements**: MusicBrainz Web Service v2 Client with Rate Limiting & Resilience  

---

```csharp
using Resonance.Core.Models;

namespace Resonance.Core.Services.Abstractions;

/// <summary>
///     Service for resolving artist, recording and release metadata via the MusicBrainz database.
/// </summary>
public interface IMusicBrainzService
{
    /// <summary>
    ///     Searches MusicBrainz for an artist by name and returns the best matching MBID.
    /// </summary>
    /// <param name="artistName">The artist name to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The MusicBrainz ID (MBID) if found, null otherwise.</returns>
    Task<string?> SearchArtistAsync(string artistName, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves canonical metadata for a specific recording by its MusicBrainz Recording ID,
    ///     including release details, tracks, disc numbers, and genres.
    /// </summary>
    /// <param name="recordingMbid">The unique MusicBrainz Recording MBID.</param>
    /// <param name="preferredAlbum">Optional preferred local album title for disambiguation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved recording details, or null if not found.</returns>
    Task<MusicBrainzRecordingDetail?> GetRecordingMetadataAsync(
        string recordingMbid, 
        string? preferredAlbum = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Searches MusicBrainz recordings by text query (artist and title) as a fallback
    ///     when no MusicBrainzTrackId is associated with the song.
    /// </summary>
    /// <param name="artist">The artist name.</param>
    /// <param name="title">The track title.</param>
    /// <param name="preferredAlbum">Optional preferred local album title for disambiguation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The best matching recording details, or null if no confident match exists.</returns>
    Task<MusicBrainzRecordingDetail?> SearchRecordingAsync(
        string artist, 
        string title, 
        string? preferredAlbum = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resolves the Cover Art Archive URL for a given release MBID.
    /// </summary>
    /// <param name="releaseMbid">The MusicBrainz Release MBID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The primary front cover image URL (500px), or null if not available.</returns>
    Task<string?> GetCoverArtUrlAsync(string releaseMbid, CancellationToken cancellationToken = default);
}
```
