using ATL;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Resonance.Core.Helpers;
using Resonance.Core.Models;
using Resonance.Core.Models.Lyrics;
using Resonance.Core.Services.Abstractions;
using Resonance.Core.Services.Implementations;
using Xunit;

namespace Resonance.Core.Tests.Services;

/// <summary>
///     Unit tests for Slice 1: Canonical local lyrics resolution precedence and parsing.
///     Precedence: Embedded Synced -> Embedded Plain -> Sidecar .lrc -> Sidecar .txt.
///     Guarantees Local-First offline resolution and zero external network calls when local lyrics exist.
/// </summary>
public class LrcServiceLocalResolutionTests
{
    private const string AudioDirectory = @"C:\Music\Pink Floyd - The Dark Side of the Moon";
    private const string AudioFilePath = @"C:\Music\Pink Floyd - The Dark Side of the Moon\01 - Time.flac";
    private const string AudioBaseName = "01 - Time";
    private const string Artist = "Pink Floyd";
    private const string Title = "Time";

    private readonly IFileSystemService _fileSystem;
    private readonly IOnlineLyricsService _onlineLyricsService;
    private readonly INetEaseLyricsService _netEaseLyricsService;
    private readonly ISettingsService _settingsService;
    private readonly IPathConfiguration _pathConfig;
    private readonly ILibraryWriter _libraryWriter;
    private readonly ILogger<LrcService> _logger;
    private readonly TestableLrcService _sut;

    public LrcServiceLocalResolutionTests()
    {
        _fileSystem = Substitute.For<IFileSystemService>();
        _onlineLyricsService = Substitute.For<IOnlineLyricsService>();
        _netEaseLyricsService = Substitute.For<INetEaseLyricsService>();
        _settingsService = Substitute.For<ISettingsService>();
        _pathConfig = Substitute.For<IPathConfiguration>();
        _libraryWriter = Substitute.For<ILibraryWriter>();
        _logger = Substitute.For<ILogger<LrcService>>();

        _fileSystem.GetDirectoryName(AudioFilePath).Returns(AudioDirectory);
        _fileSystem.GetFileNameWithoutExtension(AudioFilePath).Returns(AudioBaseName);
        _fileSystem.Combine(Arg.Any<string[]>())
            .Returns(ci => Path.Combine(ci.ArgAt<string[]>(0)));
        _fileSystem.GetFileNameWithoutExtension(Arg.Any<string>())
            .Returns(ci => Path.GetFileNameWithoutExtension(ci.ArgAt<string>(0)));

        _sut = new TestableLrcService(
            _fileSystem,
            _onlineLyricsService,
            _netEaseLyricsService,
            _settingsService,
            _pathConfig,
            _libraryWriter,
            _logger);
    }

    private Song CreateTestSong() => new()
    {
        Id = Guid.NewGuid(),
        FilePath = AudioFilePath,
        Title = Title,
        PrimaryArtistName = Artist
    };

    [Fact]
    public async Task ResolveLyricsAsync_WhenEmbeddedSyncedExists_TakesPrecedenceOverAllOtherSources()
    {
        // Arrange
        var song = CreateTestSong();
        var phases = new List<LyricsInfo.LyricsPhrase>
        {
            new(1000, "Ticking away the moments that make up a dull day"),
            new(5000, "Fritter and waste the hours in an offhand way")
        };
        _sut.MockSyncedLyrics = phases;
        _sut.MockUnsyncedLyrics = "Unsynced plain lyrics that should be ignored";

        // Setup sidecars that should also be ignored
        var exactLrc = Path.Combine(AudioDirectory, $"{AudioBaseName}.lrc");
        _fileSystem.GetFiles(AudioDirectory, "*.lrc").Returns(new[] { exactLrc });
        _fileSystem.FileExists(exactLrc).Returns(true);
        _fileSystem.ReadAllTextAsync(exactLrc).Returns("[00:02.00]Sidecar lrc line");

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(LyricsType.Synced);
        result.Provenance.Should().Be(LyricsProvenance.EmbeddedSynced);
        result.Lines.Should().HaveCount(2);
        result.Lines[0].Text.Should().Be("Ticking away the moments that make up a dull day");
        result.Lines[0].StartTime.Should().Be(TimeSpan.FromSeconds(1));
        result.Lines[1].Text.Should().Be("Fritter and waste the hours in an offhand way");
        result.Lines[1].StartTime.Should().Be(TimeSpan.FromSeconds(5));

        // Zero network calls
        await _onlineLyricsService.DidNotReceiveWithAnyArgs().GetLyricsAsync(default!, default!, default!, default, default);
        await _netEaseLyricsService.DidNotReceiveWithAnyArgs().SearchLyricsAsync(default!, default!, default);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenEmbeddedPlainExists_TakesPrecedenceOverSidecars()
    {
        // Arrange
        var song = CreateTestSong();
        _sut.MockSyncedLyrics = null;
        _sut.MockUnsyncedLyrics = "Embedded plain lyrics text\nSecond line of lyrics";

        var exactLrc = Path.Combine(AudioDirectory, $"{AudioBaseName}.lrc");
        _fileSystem.GetFiles(AudioDirectory, "*.lrc").Returns(new[] { exactLrc });
        _fileSystem.FileExists(exactLrc).Returns(true);
        _fileSystem.ReadAllTextAsync(exactLrc).Returns("[00:02.00]Sidecar lrc line");

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(LyricsType.Plain);
        result.Provenance.Should().Be(LyricsProvenance.EmbeddedPlain);
        result.RawUnsyncedLyrics.Should().Contain("Embedded plain lyrics text");
        result.Lines.Should().BeEmpty();

        await _onlineLyricsService.DidNotReceiveWithAnyArgs().GetLyricsAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenNoEmbeddedLyrics_ResolvesSidecarLrcExactName()
    {
        // Arrange
        var song = CreateTestSong();
        _sut.MockSyncedLyrics = null;
        _sut.MockUnsyncedLyrics = null;

        var exactLrc = Path.Combine(AudioDirectory, $"{AudioBaseName}.lrc");
        var fallbackLrc = Path.Combine(AudioDirectory, $"{Artist} - {Title}.lrc");

        _fileSystem.GetFiles(AudioDirectory, "*.lrc").Returns(new[] { exactLrc, fallbackLrc });
        _fileSystem.FileExists(exactLrc).Returns(true);
        _fileSystem.ReadAllTextAsync(exactLrc).Returns("[00:03.50]Exact base name line");

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(LyricsType.Synced);
        result.Provenance.Should().Be(LyricsProvenance.LocalFileLrc);
        result.SourcePath.Should().Be(exactLrc);
        result.Lines.Should().HaveCount(1);
        result.Lines[0].Text.Should().Be("Exact base name line");

        await _onlineLyricsService.DidNotReceiveWithAnyArgs().GetLyricsAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenExactLrcMissing_FallsBackToArtistTitleLrc()
    {
        // Arrange
        var song = CreateTestSong();
        _sut.MockSyncedLyrics = null;
        _sut.MockUnsyncedLyrics = null;

        var fallbackLrc = Path.Combine(AudioDirectory, $"{Artist} - {Title}.lrc");

        _fileSystem.GetFiles(AudioDirectory, "*.lrc").Returns(new[] { fallbackLrc });
        _fileSystem.FileExists(fallbackLrc).Returns(true);
        _fileSystem.ReadAllTextAsync(fallbackLrc).Returns("[00:10.00]Artist Title fallback line");

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(LyricsType.Synced);
        result.Provenance.Should().Be(LyricsProvenance.LocalFileLrc);
        result.SourcePath.Should().Be(fallbackLrc);
        result.Lines.Should().HaveCount(1);
        result.Lines[0].Text.Should().Be("Artist Title fallback line");

        await _onlineLyricsService.DidNotReceiveWithAnyArgs().GetLyricsAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenNoLrc_ResolvesSidecarTxtExactName()
    {
        // Arrange
        var song = CreateTestSong();
        _sut.MockSyncedLyrics = null;
        _sut.MockUnsyncedLyrics = null;

        _fileSystem.GetFiles(AudioDirectory, "*.lrc").Returns(Array.Empty<string>());

        var exactTxt = Path.Combine(AudioDirectory, $"{AudioBaseName}.txt");
        _fileSystem.GetFiles(AudioDirectory, "*.txt").Returns(new[] { exactTxt });
        _fileSystem.FileExists(exactTxt).Returns(true);
        _fileSystem.ReadAllTextAsync(exactTxt).Returns("Plain sidecar text line 1\nLine 2");

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(LyricsType.Plain);
        result.Provenance.Should().Be(LyricsProvenance.LocalFileTxt);
        result.SourcePath.Should().Be(exactTxt);
        result.RawUnsyncedLyrics.Should().Be("Plain sidecar text line 1\nLine 2");
        result.Lines.Should().BeEmpty();

        await _onlineLyricsService.DidNotReceiveWithAnyArgs().GetLyricsAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenExactTxtMissing_FallsBackToArtistTitleTxt()
    {
        // Arrange
        var song = CreateTestSong();
        _sut.MockSyncedLyrics = null;
        _sut.MockUnsyncedLyrics = null;

        _fileSystem.GetFiles(AudioDirectory, "*.lrc").Returns(Array.Empty<string>());

        var fallbackTxt = Path.Combine(AudioDirectory, $"{Artist} - {Title}.txt");
        _fileSystem.GetFiles(AudioDirectory, "*.txt").Returns(new[] { fallbackTxt });
        _fileSystem.FileExists(fallbackTxt).Returns(true);
        _fileSystem.ReadAllTextAsync(fallbackTxt).Returns("Fallback txt content");

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(LyricsType.Plain);
        result.Provenance.Should().Be(LyricsProvenance.LocalFileTxt);
        result.SourcePath.Should().Be(fallbackTxt);
        result.RawUnsyncedLyrics.Should().Be("Fallback txt content");

        await _onlineLyricsService.DidNotReceiveWithAnyArgs().GetLyricsAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenSidecarLrcHasNoTimestamps_TreatsAsPlainLyrics()
    {
        // Arrange
        var song = CreateTestSong();
        _sut.MockSyncedLyrics = null;
        _sut.MockUnsyncedLyrics = null;

        var exactLrc = Path.Combine(AudioDirectory, $"{AudioBaseName}.lrc");
        _fileSystem.GetFiles(AudioDirectory, "*.lrc").Returns(new[] { exactLrc });
        _fileSystem.FileExists(exactLrc).Returns(true);
        // LRC file containing text without any timestamps
        _fileSystem.ReadAllTextAsync(exactLrc).Returns("Plain text without timestamps\nSecond line");

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(LyricsType.Plain);
        result.Provenance.Should().Be(LyricsProvenance.LocalFileLrc);
        result.RawUnsyncedLyrics.Should().Contain("Plain text without timestamps");

        await _onlineLyricsService.DidNotReceiveWithAnyArgs().GetLyricsAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenNoLocalLyricsExist_ReturnsNullInSlice1()
    {
        // Arrange
        var song = CreateTestSong();
        _sut.MockSyncedLyrics = null;
        _sut.MockUnsyncedLyrics = null;
        _fileSystem.GetFiles(AudioDirectory, "*.lrc").Returns(Array.Empty<string>());
        _fileSystem.GetFiles(AudioDirectory, "*.txt").Returns(Array.Empty<string>());

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExportSidecarLrcAsync_WritesLrcFileWithAudioBaseName()
    {
        // Arrange
        var song = CreateTestSong();
        var lrcContent = "[00:01.00]Exported line";
        var expectedTargetPath = Path.Combine(AudioDirectory, $"{AudioBaseName}.lrc");

        // Act
        var success = await _sut.ExportSidecarLrcAsync(song, lrcContent);

        // Assert
        success.Should().BeTrue();
        await _fileSystem.Received(1).WriteAllTextAsync(expectedTargetPath, lrcContent);
    }

    private class TestableLrcService : LrcService
    {
        public IList<LyricsInfo.LyricsPhrase>? MockSyncedLyrics { get; set; }
        public string? MockUnsyncedLyrics { get; set; }

        public TestableLrcService(
            IFileSystemService fileSystemService,
            IOnlineLyricsService onlineLyricsService,
            INetEaseLyricsService netEaseLyricsService,
            ISettingsService settingsService,
            IPathConfiguration pathConfig,
            ILibraryWriter libraryWriter,
            ILogger<LrcService> logger)
            : base(fileSystemService, onlineLyricsService, netEaseLyricsService, settingsService, pathConfig, libraryWriter, logger)
        {
        }

        protected internal override (IList<LyricsInfo.LyricsPhrase>? SyncedLyrics, string? UnsyncedLyrics) ExtractEmbeddedLyrics(string audioFilePath)
        {
            return (MockSyncedLyrics, MockUnsyncedLyrics);
        }
    }
}
