using System.Threading.Tasks;

namespace Resonance.WinUI.Services.Abstractions;

/// <summary>
///     Defines a service for navigating between music-related entities (artists, albums, etc.).
/// </summary>
public interface IMusicNavigationService
{
    /// <summary>
    ///     Navigates to an artist's page based on a variety of input parameters.
    /// </summary>
    /// <param name="parameter">Can be a <see cref="Resonance.Core.Models.Song" />, a string (artist name),
    /// or a <see cref="Resonance.WinUI.Helpers.ArtistNavigationRequest" />.</param>
    Task NavigateToArtistAsync(object? parameter);

    /// <summary>
    ///     Navigates to an album's detail page based on a variety of input parameters.
    /// </summary>
    /// <param name="parameter">Can be a <see cref="Resonance.Core.Models.Album" />, a <see cref="Resonance.Core.Models.Song" />,
    /// a <see cref="System.Guid" /> (AlbumId), or various Album ViewModel items.</param>
    Task NavigateToAlbumAsync(object? parameter);
}
