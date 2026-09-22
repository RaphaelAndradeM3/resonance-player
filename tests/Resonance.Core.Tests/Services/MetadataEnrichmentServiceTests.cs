using FluentAssertions;
using Resonance.Core.Models;
using Resonance.Core.Services.Implementations;
using Xunit;

namespace Resonance.Core.Tests.Services;

/// <summary>
///     Unit tests for the MetadataEnrichmentService covering field-by-field diffing,
///     status classification, interactive default selection (FR-012), semantic genre merge (FR-013),
///     and Cover Art Archive provenance.
/// </summary>
public class MetadataEnrichmentServiceTests
{
    private readonly MetadataEnrichmentService _service = new();

    [Fact]
    public void CreateProposal_DetectsAllThirteenFields()
    {
        // Arrange
        var local = new TrackAudioTags
        {
            Title = "Karma Police",
            Artist = "Radiohead",
            Album = "OK Computer",
            AlbumArtist = "Radiohead",
            Year = 1997,
            TrackNumber = 6,
            TotalTracks = 12,
            DiscNumber = 1,
            TotalDiscs = 1,
            Genre = "Alternative Rock",
            Label = "Parlophone",
            Isrc = "GBAYE9700078"
        };

        var remote = new MusicBrainzRecordingDetail
        {
            RecordingId = "rec-123",
            Title = "Karma Police",
            Artist = "Radiohead",
            Album = "OK Computer",
            AlbumArtist = "Radiohead",
            Year = 1997,
            TrackNumber = 6,
            TotalTracks = 12,
            DiscNumber = 1,
            TotalDiscs = 1,
            Genres = new[] { "Alternative Rock" },
            Label = "Parlophone",
            Isrc = "GBAYE9700078",
            Score = 100
        };

        // Act
        var proposal = _service.CreateProposal("C:\\Music\\test.mp3", local, remote, "https://coverartarchive.org/release/rel-1/front-500");

        // Assert
        proposal.Proposals.Should().HaveCount(13);
        var fieldKeys = proposal.Proposals.Select(p => p.FieldKey).ToList();
        fieldKeys.Should().Contain(new[]
        {
            "Title", "Artist", "Album", "AlbumArtist", "Year",
            "TrackNumber", "TotalTracks", "DiscNumber", "TotalDiscs",
            "Genre", "Label", "Isrc", "CoverArt"
        });
    }

    [Fact]
    public void CreateProposal_WithIdenticalMetadata_SetsStatusUnchangedAndUnselected()
    {
        // Arrange
        var local = new TrackAudioTags
        {
            Title = "Paranoid Android",
            Artist = "Radiohead",
            Album = "OK Computer",
            Year = 1997,
            TrackNumber = 2
        };

        var remote = new MusicBrainzRecordingDetail
        {
            RecordingId = "rec-456",
            Title = "Paranoid Android",
            Artist = "Radiohead",
            Album = "OK Computer",
            Year = 1997,
            TrackNumber = 2
        };

        // Act
        var proposal = _service.CreateProposal("C:\\Music\\song.flac", local, remote);

        // Assert
        var titleProp = proposal.Proposals.First(p => p.FieldKey == "Title");
        titleProp.Status.Should().Be(FieldProposalStatus.Unchanged);
        titleProp.IsSelected.Should().BeFalse();

        var artistProp = proposal.Proposals.First(p => p.FieldKey == "Artist");
        artistProp.Status.Should().Be(FieldProposalStatus.Unchanged);
        artistProp.IsSelected.Should().BeFalse();

        var yearProp = proposal.Proposals.First(p => p.FieldKey == "Year");
        yearProp.Status.Should().Be(FieldProposalStatus.Unchanged);
        yearProp.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void CreateProposal_WithMissingLocalFields_SetsStatusNewValueAndSelected()
    {
        // Arrange
        var local = new TrackAudioTags
        {
            Title = "Lucky",
            // Artist, Album, Year, Label, Isrc are null
        };

        var remote = new MusicBrainzRecordingDetail
        {
            RecordingId = "rec-lucky",
            Title = "Lucky",
            Artist = "Radiohead",
            Album = "OK Computer",
            Year = 1997,
            Label = "Parlophone",
            Isrc = "GBAYE9700080"
        };

        // Act
        var proposal = _service.CreateProposal("C:\\Music\\lucky.mp3", local, remote);

        // Assert
        var artistProp = proposal.Proposals.First(p => p.FieldKey == "Artist");
        artistProp.Status.Should().Be(FieldProposalStatus.NewValue);
        artistProp.ProposedValue.Should().Be("Radiohead");
        artistProp.IsSelected.Should().BeTrue();
        artistProp.Provenance.Should().Be(MetadataProvenance.MusicBrainz);

        var albumProp = proposal.Proposals.First(p => p.FieldKey == "Album");
        albumProp.Status.Should().Be(FieldProposalStatus.NewValue);
        albumProp.ProposedValue.Should().Be("OK Computer");
        albumProp.IsSelected.Should().BeTrue();

        var labelProp = proposal.Proposals.First(p => p.FieldKey == "Label");
        labelProp.Status.Should().Be(FieldProposalStatus.NewValue);
        labelProp.ProposedValue.Should().Be("Parlophone");
        labelProp.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void CreateProposal_WithCasingDifference_SetsStatusUpdatedAndSelected()
    {
        // Arrange
        var local = new TrackAudioTags
        {
            Title = "karma police",
            Artist = "radiohead"
        };

        var remote = new MusicBrainzRecordingDetail
        {
            RecordingId = "rec-casing",
            Title = "Karma Police",
            Artist = "Radiohead"
        };

        // Act
        var proposal = _service.CreateProposal("C:\\Music\\casing.mp3", local, remote);

        // Assert
        var titleProp = proposal.Proposals.First(p => p.FieldKey == "Title");
        titleProp.Status.Should().Be(FieldProposalStatus.Updated);
        titleProp.CurrentValue.Should().Be("karma police");
        titleProp.ProposedValue.Should().Be("Karma Police");
        titleProp.IsSelected.Should().BeTrue();

        var artistProp = proposal.Proposals.First(p => p.FieldKey == "Artist");
        artistProp.Status.Should().Be(FieldProposalStatus.Updated);
        artistProp.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void CreateProposal_WithCompletelyDifferentValues_SetsStatusConflictAndUnselected()
    {
        // Arrange
        var local = new TrackAudioTags
        {
            Title = "Track 01",
            Artist = "Unknown Artist",
            Year = 2020
        };

        var remote = new MusicBrainzRecordingDetail
        {
            RecordingId = "rec-conflict",
            Title = "Airbag",
            Artist = "Radiohead",
            Year = 1997
        };

        // Act
        var proposal = _service.CreateProposal("C:\\Music\\track01.mp3", local, remote);

        // Assert - Title, Artist, and Year should be marked as Conflict with IsSelected = false (FR-012)
        var titleProp = proposal.Proposals.First(p => p.FieldKey == "Title");
        titleProp.Status.Should().Be(FieldProposalStatus.Conflict);
        titleProp.IsSelected.Should().BeFalse();

        var artistProp = proposal.Proposals.First(p => p.FieldKey == "Artist");
        artistProp.Status.Should().Be(FieldProposalStatus.Conflict);
        artistProp.IsSelected.Should().BeFalse();

        var yearProp = proposal.Proposals.First(p => p.FieldKey == "Year");
        yearProp.Status.Should().Be(FieldProposalStatus.Conflict);
        yearProp.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void CreateProposal_CoverArtResolution_AssignsCoverArtArchiveProvenance()
    {
        // Arrange
        var local = new TrackAudioTags { Title = "No Surprises" };
        var remote = new MusicBrainzRecordingDetail { RecordingId = "rec-ns", Title = "No Surprises" };
        var coverUrl = "https://coverartarchive.org/release/rel-okc/front-500";

        // Act
        var proposal = _service.CreateProposal("C:\\Music\\ns.mp3", local, remote, coverUrl, originalCoverPath: "C:\\Music\\cover.jpg");

        // Assert
        var coverProp = proposal.Proposals.First(p => p.FieldKey == "CoverArt");
        coverProp.Status.Should().Be(FieldProposalStatus.Updated);
        coverProp.Provenance.Should().Be(MetadataProvenance.CoverArtArchive);
        coverProp.ProposedValue.Should().Be(coverUrl);
        coverProp.IsSelected.Should().BeTrue();

        proposal.ProposedCoverUrl.Should().Be(coverUrl);
        proposal.ProposedCoverThumbnailUrl.Should().Be("https://coverartarchive.org/release/rel-okc/front-250");
    }

    [Fact]
    public void MergeGenres_WithLocalAndRemote_MergesAndDeduplicatesSemantically()
    {
        // Arrange
        var local = "Rock; Alternative";
        var remote = new[] { "alternative rock", "Rock", "Art Rock" };

        // Act
        var merged = _service.MergeGenres(local, remote);

        // Assert - Preserves local entries first, avoids duplicate "Rock", appends new unique tags
        merged.Should().Be("Rock; Alternative; alternative rock; Art Rock");
    }

    [Fact]
    public void MergeGenres_WithEmptyLocal_ReturnsRemoteGenresDeduplicated()
    {
        // Arrange
        var remote = new[] { "Post-Rock", "post-rock", "Ambient" };

        // Act
        var merged = _service.MergeGenres(null, remote);

        // Assert
        merged.Should().Be("Post-Rock; Ambient");
    }

    [Fact]
    public void MergeGenres_WithEmptyRemote_ReturnsLocalGenres()
    {
        // Act
        var merged = _service.MergeGenres("Electronic / Downtempo", null);

        // Assert
        merged.Should().Be("Electronic; Downtempo");
    }

    [Fact]
    public void CreateProposal_ComputedProperties_ReflectSelectedAndActionableState()
    {
        // Arrange
        var local = new TrackAudioTags { Title = "Subterranean" };
        var remote = new MusicBrainzRecordingDetail
        {
            RecordingId = "rec-sub",
            Title = "Subterranean Homesick Alien", // Variation -> Updated (IsSelected: true)
            Artist = "Radiohead",                // NewValue (IsSelected: true)
            Album = "OK Computer",               // NewValue (IsSelected: true)
            Year = 1997                          // NewValue (IsSelected: true)
        };

        // Act
        var proposal = _service.CreateProposal("C:\\Music\\sub.mp3", local, remote);

        // Assert
        proposal.HasActionableChanges.Should().BeTrue();
        proposal.SelectedCount.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void CreateProposal_FromTrackTagDetails_BehavesIdentically()
    {
        // Arrange
        var tagDetails = new TrackTagDetails
        {
            Title = "Electioneering",
            Artists = new() { "Radiohead" },
            Album = "OK Computer",
            Year = 1997
        };

        var remote = new MusicBrainzRecordingDetail
        {
            RecordingId = "rec-elec",
            Title = "Electioneering",
            Artist = "Radiohead",
            Album = "OK Computer",
            Year = 1997
        };

        // Act
        var proposal = _service.CreateProposal("C:\\Music\\elec.mp3", tagDetails, remote);

        // Assert
        proposal.Proposals.First(p => p.FieldKey == "Title").Status.Should().Be(FieldProposalStatus.Unchanged);
        proposal.Proposals.First(p => p.FieldKey == "Artist").Status.Should().Be(FieldProposalStatus.Unchanged);
        proposal.Proposals.First(p => p.FieldKey == "Year").Status.Should().Be(FieldProposalStatus.Unchanged);
    }
}
