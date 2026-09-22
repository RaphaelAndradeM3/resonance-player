using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;
using Resonance.Core.ViewModels;
using Xunit;

namespace Resonance.Core.Tests.ViewModels;

/// <summary>
///     Unit tests for <see cref="TagEditorViewModel"/> verifying initialization,
///     diff review selection, manual mode editing, cover art operations, and write plan execution.
/// </summary>
public class TagEditorViewModelTests
{
    private readonly ITagDiffService _diffService;
    private readonly ITagWriterService _writerService;
    private readonly IFilePickerService _pickerService;
    private readonly ILogger<TagEditorViewModel> _logger;

    public TagEditorViewModelTests()
    {
        _diffService = Substitute.For<ITagDiffService>();
        _writerService = Substitute.For<ITagWriterService>();
        _pickerService = Substitute.For<IFilePickerService>();
        _logger = Substitute.For<ILogger<TagEditorViewModel>>();
    }

    [Fact]
    public void InitializeFromProposal_PopulatesDiffRecordsAndMetadataCorrectly()
    {
        // Arrange
        var vm = new TagEditorViewModel(_diffService, _writerService, _pickerService, _logger);
        var proposal = new EnrichmentProposal
        {
            FilePath = "C:\\Music\\test.mp3",
            OriginalCoverPath = "C:\\Music\\cover.jpg",
            ProposedCoverUrl = "https://coverartarchive.org/release/123/front.jpg",
            Proposals = new List<FieldProposal>()
        };

        var currentTags = new TrackAudioTags
        {
            Title = "Original Title",
            Artist = "Original Artist",
            Year = 2020
        };

        var song = new Song { Id = Guid.NewGuid(), Title = "Original Title", FilePath = "C:\\Music\\test.mp3" };

        var generatedDiffs = new List<TagDiffRecord>
        {
            new()
            {
                FieldKey = "Title",
                DisplayName = "Título",
                OriginalValue = "Original Title",
                ProposedValue = "Enhanced Title",
                Status = FieldProposalStatus.Updated,
                IsSelected = true
            },
            new()
            {
                FieldKey = "Artist",
                DisplayName = "Artista",
                OriginalValue = "Original Artist",
                ProposedValue = "Original Artist",
                Status = FieldProposalStatus.Unchanged,
                IsSelected = false
            }
        };

        _diffService.GenerateDiff(proposal.FilePath, currentTags, proposal).Returns(generatedDiffs);

        // Act
        vm.InitializeFromProposal(proposal, song, currentTags);

        // Assert
        vm.IsReviewMode.Should().BeTrue();
        vm.FilePath.Should().Be("C:\\Music\\test.mp3");
        vm.CurrentSong.Should().Be(song);
        vm.DiffRecords.Should().HaveCount(2);
        vm.SelectedCount.Should().Be(1);
        vm.TotalDiffCount.Should().Be(1);
        vm.HasProposedCover.Should().BeTrue();
        vm.IncludeCoverArt.Should().BeTrue();
    }

    [Fact]
    public void InitializeForManualEdit_SetsManualModeAndPopulatesEditableModel()
    {
        // Arrange
        var vm = new TagEditorViewModel(_diffService, _writerService, _pickerService, _logger);
        var song = new Song
        {
            Id = Guid.NewGuid(),
            Title = "Local Title",
            ArtistName = "Local Artist",
            FilePath = "C:\\Music\\manual.mp3",
            AlbumArtUriFromTrack = "C:\\Music\\art.jpg"
        };

        var tags = new TrackAudioTags
        {
            Title = "Local Title",
            Artist = "Local Artist",
            Album = "Local Album",
            Year = 2018,
            TrackNumber = 4
        };

        // Act
        vm.InitializeForManualEdit(song, tags);

        // Assert
        vm.IsReviewMode.Should().BeFalse();
        vm.FilePath.Should().Be("C:\\Music\\manual.mp3");
        vm.CurrentSong.Should().Be(song);
        vm.EditableTags.Title.Should().Be("Local Title");
        vm.EditableTags.Artist.Should().Be("Local Artist");
        vm.EditableTags.Album.Should().Be("Local Album");
        vm.EditableTags.Year.Should().Be(2018);
        vm.EditableTags.TrackNumber.Should().Be(4);
        vm.DiffRecords.Should().BeEmpty();
        vm.HasCurrentCover.Should().BeTrue();
        vm.HasProposedCover.Should().BeFalse();
    }

    [Fact]
    public void ToggleSelectAll_TogglesSelectionOfChangedFields()
    {
        // Arrange
        var vm = new TagEditorViewModel(_diffService, _writerService, _pickerService, _logger);
        var r1 = new TagDiffRecord
        {
            FieldKey = "Title",
            DisplayName = "Título",
            OriginalValue = "Old",
            ProposedValue = "New",
            Status = FieldProposalStatus.Updated,
            IsSelected = false
        };
        var r2 = new TagDiffRecord
        {
            FieldKey = "Artist",
            DisplayName = "Artista",
            OriginalValue = "Same",
            ProposedValue = "Same",
            Status = FieldProposalStatus.Unchanged,
            IsSelected = false
        };

        vm.DiffRecords.Add(r1);
        vm.DiffRecords.Add(r2);

        // Act 1: Toggle should select all changed records
        vm.ToggleSelectAll();
        r1.IsSelected.Should().BeTrue();
        r2.IsSelected.Should().BeFalse(); // Unchanged should not be selected

        // Act 2: Toggle again should unselect all
        vm.ToggleSelectAll();
        r1.IsSelected.Should().BeFalse();
        r2.IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_InReviewMode_CreatesPlanAndExecutesWriterSuccessfully()
    {
        // Arrange
        var vm = new TagEditorViewModel(_diffService, _writerService, _pickerService, _logger);
        var proposal = new EnrichmentProposal
        {
            FilePath = "C:\\Music\\review.mp3",
            Proposals = new List<FieldProposal>()
        };

        var currentTags = new TrackAudioTags { Title = "A" };
        var diffs = new List<TagDiffRecord>
        {
            new() { FieldKey = "Title", Status = FieldProposalStatus.Updated, IsSelected = true }
        };

        _diffService.GenerateDiff(proposal.FilePath, currentTags, proposal).Returns(diffs);

        var expectedPlan = new TagWritePlan
        {
            FilePath = "C:\\Music\\review.mp3",
            SelectedChanges = diffs
        };

        _diffService.CreateWritePlan("C:\\Music\\review.mp3", Arg.Any<IEnumerable<TagDiffRecord>>(), null, null, false, null)
            .Returns(expectedPlan);

        _writerService.ApplyWritePlanAsync(expectedPlan)
            .Returns(TagWriteResult.Succeeded("C:\\Music\\review.mp3", 1, false, TimeSpan.FromMilliseconds(50)));

        vm.InitializeFromProposal(proposal, null, currentTags);

        bool? closedWithSuccess = null;
        vm.RequestClose += saved => closedWithSuccess = saved;

        // Act
        await vm.SaveAsync();

        // Assert
        vm.SaveSucceeded.Should().BeTrue();
        vm.HasStatusError.Should().BeFalse();
        closedWithSuccess.Should().BeTrue();
        await _writerService.Received(1).ApplyWritePlanAsync(expectedPlan);
    }

    [Fact]
    public async Task SaveAsync_InManualMode_GeneratesDiffFromModelAndAppliesPlan()
    {
        // Arrange
        var vm = new TagEditorViewModel(_diffService, _writerService, _pickerService, _logger);
        var song = new Song { Id = Guid.NewGuid(), FilePath = "C:\\Music\\manual.mp3", Title = "Old Title" };
        var tags = new TrackAudioTags { Title = "Old Title" };

        vm.InitializeForManualEdit(song, tags);
        vm.EditableTags.Title = "Edited Title";

        var manualDiffs = new List<TagDiffRecord>
        {
            new() { FieldKey = "Title", OriginalValue = "Old Title", ProposedValue = "Edited Title", Status = FieldProposalStatus.Updated, IsSelected = true }
        };

        _diffService.GenerateDiff("C:\\Music\\manual.mp3", tags, vm.EditableTags).Returns(manualDiffs);

        var expectedPlan = new TagWritePlan
        {
            FilePath = "C:\\Music\\manual.mp3",
            SongId = song.Id,
            SelectedChanges = manualDiffs
        };

        _diffService.CreateWritePlan("C:\\Music\\manual.mp3", manualDiffs, null, null, false, song.Id)
            .Returns(expectedPlan);

        _writerService.ApplyWritePlanAsync(expectedPlan)
            .Returns(TagWriteResult.Succeeded("C:\\Music\\manual.mp3", 1, false, TimeSpan.FromMilliseconds(40)));

        bool? closedWithSuccess = null;
        vm.RequestClose += saved => closedWithSuccess = saved;

        // Act
        await vm.SaveAsync();

        // Assert
        vm.SaveSucceeded.Should().BeTrue();
        closedWithSuccess.Should().BeTrue();
        await _writerService.Received(1).ApplyWritePlanAsync(expectedPlan);
    }

    [Fact]
    public async Task SaveAsync_WhenWriterFails_SetsHasStatusErrorAndDoesNotClose()
    {
        // Arrange
        var vm = new TagEditorViewModel(_diffService, _writerService, _pickerService, _logger);
        var proposal = new EnrichmentProposal { FilePath = "C:\\Music\\fail.mp3", Proposals = new List<FieldProposal>() };
        var tags = new TrackAudioTags();
        var diffs = new List<TagDiffRecord>();

        _diffService.GenerateDiff("C:\\Music\\fail.mp3", tags, proposal).Returns(diffs);
        var plan = new TagWritePlan { FilePath = "C:\\Music\\fail.mp3", SelectedChanges = diffs };
        _diffService.CreateWritePlan("C:\\Music\\fail.mp3", Arg.Any<IEnumerable<TagDiffRecord>>(), null, null, false, null)
            .Returns(plan);

        _writerService.ApplyWritePlanAsync(plan)
            .Returns(TagWriteResult.Failed("C:\\Music\\fail.mp3", "Erro de E/S no disco.", TimeSpan.FromMilliseconds(20)));

        vm.InitializeFromProposal(proposal, null, tags);

        bool? closed = null;
        vm.RequestClose += saved => closed = saved;

        // Act
        await vm.SaveAsync();

        // Assert
        vm.SaveSucceeded.Should().BeFalse();
        vm.HasStatusError.Should().BeTrue();
        vm.StatusMessage.Should().Contain("Erro de E/S no disco.");
        closed.Should().BeNull(); // Dialog stays open on error so user can review/retry
    }

    [Fact]
    public void RemoveCover_SetsRemovePictureFlagAndClearsProposedCover()
    {
        // Arrange
        var vm = new TagEditorViewModel(_diffService, _writerService, _pickerService, _logger);
        vm.ProposedCoverUri = "https://example.com/cover.jpg";
        vm.NewPictureBytes = new byte[] { 1, 2, 3 };
        vm.IncludeCoverArt = true;

        // Act
        vm.RemoveCover();

        // Assert
        vm.RemovePicture.Should().BeTrue();
        vm.NewPictureBytes.Should().BeNull();
        vm.ProposedCoverUri.Should().BeNull();
        vm.IncludeCoverArt.Should().BeFalse();
        vm.HasProposedCover.Should().BeFalse();
    }

    [Fact]
    public void Cancel_TriggersRequestCloseWithFalse()
    {
        // Arrange
        var vm = new TagEditorViewModel(_diffService, _writerService, _pickerService, _logger);
        bool? closed = null;
        vm.RequestClose += saved => closed = saved;

        // Act
        vm.Cancel();

        // Assert
        closed.Should().BeFalse();
    }
}
