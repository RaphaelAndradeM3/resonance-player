using Resonance.Core.Models;

namespace Resonance.Core.Services.Abstractions;

/// <summary>
///     Defines the contract for generating field-by-field diff records between current audio tags
///     and proposed modifications (either online enrichment proposals or manual edits),
///     and building an actionable <see cref="TagWritePlan"/>.
/// </summary>
public interface ITagDiffService
{
    /// <summary>
    ///     Generates a field-by-field diff comparison between current local tags and an online enrichment proposal.
    /// </summary>
    /// <param name="filePath">The physical path to the audio file.</param>
    /// <param name="currentTags">Current audio tags read from the file or database.</param>
    /// <param name="proposal">The enrichment proposal containing provider suggestions.</param>
    /// <returns>A collection of <see cref="TagDiffRecord"/> items with appropriate status and selection flags.</returns>
    IReadOnlyList<TagDiffRecord> GenerateDiff(
        string filePath,
        TrackAudioTags currentTags,
        EnrichmentProposal proposal);

    /// <summary>
    ///     Generates a field-by-field diff comparison between original tags and manually edited values.
    /// </summary>
    /// <param name="filePath">The physical path to the audio file.</param>
    /// <param name="originalTags">Original audio tags read from the file.</param>
    /// <param name="editedModel">Values edited manually by the user in the form.</param>
    /// <returns>A collection of <see cref="TagDiffRecord"/> items with changed fields marked as selected.</returns>
    IReadOnlyList<TagDiffRecord> GenerateDiff(
        string filePath,
        TrackAudioTags originalTags,
        EditableTagModel editedModel);

    /// <summary>
    ///     Builds an immutable <see cref="TagWritePlan"/> containing only the approved changes where <see cref="TagDiffRecord.IsSelected"/> is true.
    /// </summary>
    /// <param name="filePath">The physical path to the audio file.</param>
    /// <param name="diffRecords">The full collection of diff records.</param>
    /// <param name="newPictureBytes">Optional bytes of the new cover image to embed.</param>
    /// <param name="pictureMimeType">Optional MIME type of the cover image.</param>
    /// <param name="removePicture">Whether to explicitly remove existing cover art.</param>
    /// <param name="songId">Optional library song identifier.</param>
    /// <returns>A validated <see cref="TagWritePlan"/>.</returns>
    TagWritePlan CreateWritePlan(
        string filePath,
        IEnumerable<TagDiffRecord> diffRecords,
        byte[]? newPictureBytes = null,
        string? pictureMimeType = null,
        bool removePicture = false,
        Guid? songId = null);
}
