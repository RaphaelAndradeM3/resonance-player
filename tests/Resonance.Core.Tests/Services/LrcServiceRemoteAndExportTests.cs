using System.Net;
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
///     Integration & unit tests for Slice 2:
///     - Stage 5 (Local cache in %LocalAppData%)
///     - Stage 6 (Remote providers: LRCLIB synced/plain/instrumental, NetEase, caching and rate-limiting)
///     - Instrumental persistence in database
///     - Lyrics timing offset persistence
///     - Explicit sidecar export to music directory
/// </summary>
public class LrcServiceRemoteAndExportTests
{
    private const string CacheDirectory = @"C:\Users\User\AppData\Local\Resonance\Cache\Lrc";
    private const string AudioDirectory = @"C:\Music\Pink Floyd - The Dark Side of the Moon";
    private const string AudioFilePath = @"C:\Music\Pink Floyd - The Dark Side of the Moon\01 - Speak to Me.flac";
    private const string AudioBaseName = "01 - Speak to Me";
    private const string Artist = "Pink Floyd";
    private const string Title = "Speak to Me";

    private readonly IFileSystemService _fileSystem;
    private readonly IOnlineLyricsService _onlineLyricsService;
    private readonly INetEaseLyricsService _netEaseLyricsService;
    private readonly ISettingsService _settingsService;
    private readonly IPathConfiguration _pathConfig;
    private readonly ILibraryWriter _libraryWriter;
    private readonly ILogger<LrcService> _logger;
    private readonly TestableLrcService _sut;

    public LrcServiceRemoteAndExportTests()
    {
        _fileSystem = Substitute.For<IFileSystemService>();
        _onlineLyricsService = Substitute.For<IOnlineLyricsService>();
        _netEaseLyricsService = Substitute.For<INetEaseLyricsService>();
        _settingsService = Substitute.For<ISettingsService>();
        _pathConfig = Substitute.For<IPathConfiguration>();
        _libraryWriter = Substitute.For<ILibraryWriter>();
        _logger = Substitute.For<ILogger<LrcService>>();

        _pathConfig.LrcCachePath.Returns(CacheDirectory);
        _settingsService.GetFetchOnlineLyricsEnabledAsync().Returns(true);
        _settingsService.GetEnabledServiceProvidersAsync(ServiceCategory.Lyrics).Returns(new List<ServiceProviderSetting>
        {
            new() { Id = ServiceProviderIds.LrcLib, DisplayName = "LRCLIB", Category = ServiceCategory.Lyrics, IsEnabled = true, Order = 0 },
            new() { Id = ServiceProviderIds.NetEase, DisplayName = "NetEase", Category = ServiceCategory.Lyrics, IsEnabled = true, Order = 1 }
        });

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
    public async Task ResolveLyricsAsync_WhenFlaggedAsInstrumentalInDb_ReturnsInstrumentalWithoutRemoteCalls()
    {
        // Arrange
        var song = CreateTestSong();
        song.IsInstrumental = true;

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(LyricsType.Instrumental);
        result.IsInstrumental.Should().BeTrue();
        result.Provenance.Should().Be(LyricsProvenance.LocalCache);

        await _onlineLyricsService.DidNotReceiveWithAnyArgs().GetLyricsResultAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenCachedLyricsExistInAppData_ReturnsCachedLyricsWithoutRemoteCalls()
    {
        // Arrange
        var song = CreateTestSong();
        var cachedFilePath = Path.Combine(CacheDirectory, "cached_identity.lrc");
        song.LrcFilePath = cachedFilePath;
        _fileSystem.FileExists(cachedFilePath).Returns(true);
        _fileSystem.ReadAllTextAsync(cachedFilePath).Returns("[00:01.00]Cached line 1\n[00:04.00]Cached line 2");

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(LyricsType.Synced);
        result.Provenance.Should().Be(LyricsProvenance.LocalCache);
        result.SourcePath.Should().Be(cachedFilePath);
        result.Lines.Should().HaveCount(2);

        await _onlineLyricsService.DidNotReceiveWithAnyArgs().GetLyricsResultAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenOnlineDisabled_DoesNotQueryRemoteProviders()
    {
        // Arrange
        var song = CreateTestSong();
        _settingsService.GetFetchOnlineLyricsEnabledAsync().Returns(false);

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().BeNull();
        await _onlineLyricsService.DidNotReceiveWithAnyArgs().GetLyricsResultAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenAlreadyCheckedBefore_DoesNotQueryRemoteProviders()
    {
        // Arrange
        var song = CreateTestSong();
        song.LyricsLastCheckedUtc = DateTime.UtcNow.AddDays(-1);

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().BeNull();
        await _onlineLyricsService.DidNotReceiveWithAnyArgs().GetLyricsResultAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenLrcLibReturnsSyncedLyrics_CachesLocallyAndReturnsDocument()
    {
        // Arrange
        var song = CreateTestSong();
        var lrcText = "[00:01.50]I've been mad for fucking years\n[00:05.00]Absolutely years";
        _onlineLyricsService.GetLyricsResultAsync(song.Title, song.PrimaryArtistName, song.Album?.Title, song.Duration, Arg.Any<CancellationToken>())
            .Returns(new OnlineLyricsResult
            {
                SyncedLyrics = lrcText,
                IsInstrumental = false
            });

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(LyricsType.Synced);
        result.Provenance.Should().Be(LyricsProvenance.RemoteLrcLib);
        result.Lines.Should().HaveCount(2);

        // Verify written to cache and database updated
        await _fileSystem.Received(1).WriteAllTextAsync(Arg.Is<string>(p => p.StartsWith(CacheDirectory)), lrcText);
        await _libraryWriter.Received(1).UpdateSongLyricsLastCheckedAsync(song.Id);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenLrcLibReturnsInstrumental_UpdatesDatabaseAndReturnsInstrumentalDocument()
    {
        // Arrange
        var song = CreateTestSong();
        _onlineLyricsService.GetLyricsResultAsync(song.Title, song.PrimaryArtistName, song.Album?.Title, song.Duration, Arg.Any<CancellationToken>())
            .Returns(new OnlineLyricsResult
            {
                IsInstrumental = true
            });

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(LyricsType.Instrumental);
        result.IsInstrumental.Should().BeTrue();
        result.Provenance.Should().Be(LyricsProvenance.RemoteLrcLib);

        song.IsInstrumental.Should().BeTrue();
        await _libraryWriter.Received(1).UpdateSongInstrumentalAsync(song.Id, true);
        await _libraryWriter.Received(1).UpdateSongLyricsLastCheckedAsync(song.Id);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenLrcLibReturnsPlainLyrics_CachesLocallyAndReturnsPlainDocument()
    {
        // Arrange
        var song = CreateTestSong();
        var plainText = "Plain lyrics without timestamps\nSecond line";
        _onlineLyricsService.GetLyricsResultAsync(song.Title, song.PrimaryArtistName, song.Album?.Title, song.Duration, Arg.Any<CancellationToken>())
            .Returns(new OnlineLyricsResult
            {
                PlainLyrics = plainText,
                IsInstrumental = false
            });

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(LyricsType.Plain);
        result.Provenance.Should().Be(LyricsProvenance.RemoteLrcLib);
        result.RawUnsyncedLyrics.Should().Be(plainText);
        result.Lines.Should().BeEmpty();

        await _fileSystem.Received(1).WriteAllTextAsync(Arg.Is<string>(p => p.StartsWith(CacheDirectory)), plainText);
        await _libraryWriter.Received(1).UpdateSongLyricsLastCheckedAsync(song.Id);
    }

    [Fact]
    public async Task ResolveLyricsAsync_WhenAllRemoteProvidersFail_UpdatesLyricsLastCheckedUtc()
    {
        // Arrange
        var song = CreateTestSong();
        _onlineLyricsService.GetLyricsResultAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns((OnlineLyricsResult?)null);
        _netEaseLyricsService.SearchLyricsAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        var result = await _sut.ResolveLyricsAsync(song);

        // Assert
        result.Should().BeNull();
        song.LyricsLastCheckedUtc.Should().NotBeNull();
        await _libraryWriter.Received(1).UpdateSongLyricsLastCheckedAsync(song.Id);
    }

    [Fact]
    public async Task SetLyricsOffsetAsync_UpdatesSongAndPersistsToLibraryWriter()
    {
        // Arrange
        var song = CreateTestSong();
        var newOffsetMs = 250;

        // Act
        await _sut.SetLyricsOffsetAsync(song, newOffsetMs);

        // Assert
        song.LyricsOffsetMs.Should().Be(250);
        await _libraryWriter.Received(1).UpdateSongLyricsOffsetAsync(song.Id, 250);
    }

    [Fact]
    public async Task ExportSidecarLrcAsync_ExportsContentToMediaDirectory()
    {
        // Arrange
        var song = CreateTestSong();
        var content = "[00:01.00]Exported line";
        var expectedPath = Path.Combine(AudioDirectory, $"{AudioBaseName}.lrc");

        // Act
        var success = await _sut.ExportSidecarLrcAsync(song, content);

        // Assert
        success.Should().BeTrue();
        await _fileSystem.Received(1).WriteAllTextAsync(expectedPath, content);
    }

    private class TestableLrcService : LrcService
    {
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

        protected internal override (IList<ATL.LyricsInfo.LyricsPhrase>? SyncedLyrics, string? UnsyncedLyrics) ExtractEmbeddedLyrics(string audioFilePath)
        {
            return (null, null);
        }
    }
}
