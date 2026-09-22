using CommunityToolkit.Mvvm.ComponentModel;

namespace Resonance.Core.Models;

/// <summary>
///     Mutable model representing audio metadata fields for manual editing in the Tag Editor.
/// </summary>
public partial class EditableTagModel : ObservableObject
{
    [ObservableProperty] private string? _title;
    [ObservableProperty] private string? _artist;
    [ObservableProperty] private string? _album;
    [ObservableProperty] private string? _albumArtist;
    [ObservableProperty] private string? _year;
    [ObservableProperty] private string? _trackNumber;
    [ObservableProperty] private string? _trackTotal;
    [ObservableProperty] private string? _discNumber;
    [ObservableProperty] private string? _discTotal;
    [ObservableProperty] private string? _genre;
    [ObservableProperty] private string? _comment;
    [ObservableProperty] private byte[]? _pictureBytes;
    [ObservableProperty] private string? _pictureMimeType;

    /// <summary>
    ///     Creates a populated <see cref="EditableTagModel"/> from existing <see cref="TrackAudioTags"/>.
    /// </summary>
    public static EditableTagModel FromTrackAudioTags(TrackAudioTags tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return new EditableTagModel
        {
            Title = tags.Title,
            Artist = tags.Artist,
            Album = tags.Album,
            AlbumArtist = tags.AlbumArtist,
            Year = tags.Year > 0 ? tags.Year.Value.ToString() : null,
            TrackNumber = tags.TrackNumber > 0 ? tags.TrackNumber.Value.ToString() : null,
            TrackTotal = tags.TotalTracks > 0 ? tags.TotalTracks.Value.ToString() : null,
            DiscNumber = tags.DiscNumber > 0 ? tags.DiscNumber.Value.ToString() : null,
            DiscTotal = tags.TotalDiscs > 0 ? tags.TotalDiscs.Value.ToString() : null,
            Genre = tags.Genre,
            Comment = tags.Comment
        };
    }
}
