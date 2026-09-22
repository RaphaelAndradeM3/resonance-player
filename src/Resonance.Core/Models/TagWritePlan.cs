namespace Resonance.Core.Models;

/// <summary>
///     Encapsulates an immutable, validated plan for safely writing selected tag changes to an audio file.
/// </summary>
public class TagWritePlan
{
    /// <summary>Gets the absolute physical file path to update.</summary>
    public required string FilePath { get; init; }

    /// <summary>Gets the optional library database identifier for the song.</summary>
    public Guid? SongId { get; init; }

    /// <summary>Gets the collection of user-approved field modifications.</summary>
    public required IReadOnlyList<TagDiffRecord> SelectedChanges { get; init; }

    /// <summary>Gets raw binary bytes of the new cover image to embed, if approved.</summary>
    public byte[]? NewPictureBytes { get; init; }

    /// <summary>Gets the MIME type of the new cover image (e.g. image/jpeg, image/png).</summary>
    public string? PictureMimeType { get; init; }

    /// <summary>Gets whether any existing embedded picture should be removed without replacement.</summary>
    public bool RemovePicture { get; init; }

    /// <summary>Gets the timestamp when this plan was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets whether this plan contains at least one actionable modification.</summary>
    public bool HasChanges => SelectedChanges.Count > 0 || NewPictureBytes != null || RemovePicture;
}
