using FluentAssertions;
using Resonance.Core.Models;
using Resonance.Core.Services.Implementations;
using Xunit;

namespace Resonance.Core.Tests.Services;

/// <summary>
///     Unit tests for the TagDiffService validating field diffing, status classification,
///     manual model comparisons, and TagWritePlan generation.
/// </summary>
public class TagDiffServiceTests
{
    private readonly TagDiffService _service = new();

    [Fact]
    public void GenerateDiff_FromEnrichmentProposal_MapsAllFieldsAndCoverArt()
    {
        // Arrange
        var local = new TrackAudioTags
        {
            Title = "Original Title",
            Artist = "Original Artist",
            Year = 2000
        };

        var proposal = new EnrichmentProposal
        {
            FilePath = "C:\\Music\\song.mp3",
            OriginalCoverPath = "C:\\Music\\cover.jpg",
            ProposedCoverUrl = "https://coverartarchive.org/release/123/front-500",
            Proposals = new List<FieldProposal>
            {
                new()
                {
                    FieldName = "Título",
                    FieldKey = "Title",
                    CurrentValue = "Original Title",
                    ProposedValue = "Original Title",
                    Status = FieldProposalStatus.Unchanged,
                    Provenance = MetadataProvenance.LocalTag,
                    IsSelected = false
                },
                new()
                {
                    FieldName = "Artista",
                    FieldKey = "Artist",
                    CurrentValue = "Original Artist",
                    ProposedValue = "Remastered Artist",
                    Status = FieldProposalStatus.Updated,
                    Provenance = MetadataProvenance.MusicBrainz,
                    IsSelected = true
                },
                new()
                {
                    FieldName = "Ano",
                    FieldKey = "Year",
                    CurrentValue = "2000",
                    ProposedValue = "2001",
                    Status = FieldProposalStatus.Updated,
                    Provenance = MetadataProvenance.MusicBrainz,
                    IsSelected = true
                }
            }
        };

        // Act
        var diff = _service.GenerateDiff("C:\\Music\\song.mp3", local, proposal);

        // Assert
        diff.Should().HaveCount(4); // 3 text fields + 1 cover art
        var titleRecord = diff.First(d => d.FieldKey == "Title");
        titleRecord.Status.Should().Be(FieldProposalStatus.Unchanged);
        titleRecord.IsSelected.Should().BeFalse();

        var artistRecord = diff.First(d => d.FieldKey == "Artist");
        artistRecord.Status.Should().Be(FieldProposalStatus.Updated);
        artistRecord.ProposedValue.Should().Be("Remastered Artist");
        artistRecord.IsSelected.Should().BeTrue();

        var coverRecord = diff.First(d => d.FieldKey == "CoverArt");
        coverRecord.IsPictureField.Should().BeTrue();
        coverRecord.Status.Should().Be(FieldProposalStatus.Updated);
        coverRecord.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void GenerateDiff_FromEditableModel_CorrectlyIdentifiesUnchangedNewAndUpdated()
    {
        // Arrange
        var original = new TrackAudioTags
        {
            Title = "Song A",
            Artist = "Artist A",
            Album = null,
            Year = 2010
        };

        var edited = new EditableTagModel
        {
            Title = "Song A",         // Unchanged
            Artist = "Artist B",       // Updated
            Album = "New Album",       // NewValue
            Year = "2010"              // Unchanged
        };

        // Act
        var diff = _service.GenerateDiff("C:\\Music\\song.mp3", original, edited);

        // Assert
        var titleDiff = diff.First(d => d.FieldKey == "Title");
        titleDiff.Status.Should().Be(FieldProposalStatus.Unchanged);
        titleDiff.IsSelected.Should().BeFalse();

        var artistDiff = diff.First(d => d.FieldKey == "Artist");
        artistDiff.Status.Should().Be(FieldProposalStatus.Updated);
        artistDiff.OriginalValue.Should().Be("Artist A");
        artistDiff.ProposedValue.Should().Be("Artist B");
        artistDiff.IsSelected.Should().BeTrue();

        var albumDiff = diff.First(d => d.FieldKey == "Album");
        albumDiff.Status.Should().Be(FieldProposalStatus.NewValue);
        albumDiff.OriginalValue.Should().BeNull();
        albumDiff.ProposedValue.Should().Be("New Album");
        albumDiff.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void CreateWritePlan_FiltersOnlySelectedAndChangedRecords()
    {
        // Arrange
        var records = new List<TagDiffRecord>
        {
            new() { FieldKey = "Title", Status = FieldProposalStatus.Unchanged, IsSelected = false },
            new() { FieldKey = "Artist", Status = FieldProposalStatus.Updated, ProposedValue = "New Artist", IsSelected = true },
            new() { FieldKey = "Album", Status = FieldProposalStatus.NewValue, ProposedValue = "New Album", IsSelected = false }, // Not selected!
            new() { FieldKey = "Year", Status = FieldProposalStatus.Updated, ProposedValue = "2024", IsSelected = true }
        };

        var pictureBytes = new byte[] { 0xFF, 0xD8, 0xFF };

        // Act
        var plan = _service.CreateWritePlan("C:\\Music\\song.flac", records, newPictureBytes: pictureBytes, pictureMimeType: "image/jpeg");

        // Assert
        plan.FilePath.Should().Be("C:\\Music\\song.flac");
        plan.SelectedChanges.Should().HaveCount(2); // Only Artist and Year
        plan.SelectedChanges.Select(c => c.FieldKey).Should().BeEquivalentTo(new[] { "Artist", "Year" });
        plan.NewPictureBytes.Should().BeEquivalentTo(pictureBytes);
        plan.PictureMimeType.Should().Be("image/jpeg");
        plan.HasChanges.Should().BeTrue();
    }
}
