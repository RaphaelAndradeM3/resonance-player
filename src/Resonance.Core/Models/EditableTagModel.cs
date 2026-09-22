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
    [ObservableProperty] private uint? _year;
    [ObservableProperty] private uint? _trackNumber;
    [ObservableProperty] private uint? _trackTotal;
    [ObservableProperty] private uint? _discNumber;
    [ObservableProperty] private uint? _discTotal;
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
            Year = tags.Year > 0 ? (uint)tags.Year.Value : null,
            TrackNumber = tags.TrackNumber > 0 ? (uint)tags.TrackNumber.Value : null,
            TrackTotal = tags.TotalTracks > 0 ? (uint)tags.TotalTracks.Value : null,
            DiscNumber = tags.DiscNumber > 0 ? (uint)tags.DiscNumber.Value : null,
            DiscTotal = tags.TotalDiscs > 0 ? (uint)tags.TotalDiscs.Value : null,
            Genre = tags.Genre,
            Comment = tags.Comment
        };
    }
}
