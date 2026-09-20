using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Resonance.Core.Helpers;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;
using Resonance.Core.Services.Data;
using Resonance.Core.Services.Implementations;
using Resonance.Core.Tests.Utils;
using Xunit;

namespace Resonance.Core.Tests;

public class FolderRecursivePlaybackTests : IDisposable
{
    private readonly DbContextFactoryTestHelper _dbHelper;
    private readonly LibraryService _libraryService;

    public FolderRecursivePlaybackTests()
    {
        _dbHelper = new DbContextFactoryTestHelper();

        var fileSystem = Substitute.For<IFileSystemService>();
        var metadataService = Substitute.For<IMetadataService>();
        var lastFmService = Substitute.For<ILastFmMetadataService>();
        var musicBrainzService = Substitute.For<IMusicBrainzService>();
        var fanartTvService = Substitute.For<IFanartTvService>();
        var theAudioDbService = Substitute.For<ITheAudioDbService>();
        var httpClientFactory = Substitute.For<System.Net.Http.IHttpClientFactory>();
        var serviceScopeFactory = Substitute.For<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        var pathConfig = Substitute.For<IPathConfiguration>();
        var settingsService = Substitute.For<ISettingsService>();
        var replayGainService = Substitute.For<IReplayGainService>();
        var apiKeyService = Substitute.For<IApiKeyService>();
        var imageProcessor = Substitute.For<IImageProcessor>();
        var pipelines = TestProviderPipeline.Build(ServiceProviderIds.ImageDownload);
        var logger = Substitute.For<ILogger<LibraryService>>();

        _libraryService = new LibraryService(
            _dbHelper.ContextFactory,
            fileSystem,
            metadataService,
            lastFmService,
            musicBrainzService,
            fanartTvService,
            theAudioDbService,
            httpClientFactory,
            serviceScopeFactory,
            pathConfig,
            settingsService,
            replayGainService,
            apiKeyService,
            imageProcessor,
            pipelines,
            logger);
    }

    public void Dispose()
    {
        _libraryService.Dispose();
        _dbHelper.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetAllSongIdsInDirectoryRecursiveAsync_FindsSongsInNestedFolders()
    {
        var rootFolder = new Folder
        {
            Id = Guid.NewGuid(),
            Name = "MP3",
            Path = @"H:\MP3",
            ParentFolderId = null
        };

        var subFolder1 = new Folder
        {
            Id = Guid.NewGuid(),
            Name = "098 - Original",
            Path = @"H:\MP3\098 - Original",
            ParentFolderId = rootFolder.Id
        };

        var subFolder2 = new Folder
        {
            Id = Guid.NewGuid(),
            Name = "005 - Raphael Web 2020",
            Path = @"H:\MP3\098 - Original\005 - Raphael Web 2020",
            ParentFolderId = subFolder1.Id
        };

        var subFolder3 = new Folder
        {
            Id = Guid.NewGuid(),
            Name = "006 - Raphael ID2",
            Path = @"H:\MP3\098 - Original\006 - Raphael ID2",
            ParentFolderId = subFolder1.Id
        };

        var song1 = new Song
        {
            Id = Guid.NewGuid(),
            Title = "Song in 005",
            FilePath = @"H:\MP3\098 - Original\005 - Raphael Web 2020\track1.mp3",
            DirectoryPath = @"H:\MP3\098 - Original\005 - Raphael Web 2020",
            FolderId = rootFolder.Id
        };

        var song2 = new Song
        {
            Id = Guid.NewGuid(),
            Title = "Song in 006",
            FilePath = @"H:\MP3\098 - Original\006 - Raphael ID2\track2.mp3",
            DirectoryPath = @"H:\MP3\098 - Original\006 - Raphael ID2",
            FolderId = rootFolder.Id
        };

        await using (var context = await _dbHelper.ContextFactory.CreateDbContextAsync())
        {
            context.Folders.AddRange(rootFolder, subFolder1, subFolder2, subFolder3);
            context.Songs.AddRange(song1, song2);
            await context.SaveChangesAsync();
        }

        // Test 1: Recursive retrieval from subFolder1 ("098 - Original")
        var songIds = await _libraryService.GetAllSongIdsInDirectoryRecursiveAsync(
            rootFolder.Id, subFolder1.Path, SongSortOrder.TitleAsc);

        songIds.Should().HaveCount(2);
        songIds.Should().Contain(song1.Id);
        songIds.Should().Contain(song2.Id);

        // Test 2: Recursive retrieval from subFolder2 ("005 - Raphael Web 2020")
        var songIds2 = await _libraryService.GetAllSongIdsInDirectoryRecursiveAsync(
            rootFolder.Id, subFolder2.Path, SongSortOrder.TitleAsc);

        songIds2.Should().HaveCount(1);
        songIds2.Should().Contain(song1.Id);

        // Test 3: Retrieval via GetAllSongIdsByFolderIdAsync with subfolder 1 ID
        var songIdsByFolderId = await _libraryService.GetAllSongIdsByFolderIdAsync(
            subFolder1.Id, SongSortOrder.TitleAsc);
        songIdsByFolderId.Should().HaveCount(2);

        // Test 4: Retrieval via GetAllSongIdsByFolderIdAsync with subfolder 2 ID ("005 - Raphael Web 2020")
        var songIdsBySubfolder2 = await _libraryService.GetAllSongIdsByFolderIdAsync(
            subFolder2.Id, SongSortOrder.TitleAsc);
        songIdsBySubfolder2.Should().HaveCount(1);
        songIdsBySubfolder2.Should().Contain(song1.Id);

        // Test 5: Retrieval via GetAllSongIdsByFolderIdAsync with root folder ID
        var songIdsByRoot = await _libraryService.GetAllSongIdsByFolderIdAsync(
            rootFolder.Id, SongSortOrder.TitleAsc);
        songIdsByRoot.Should().HaveCount(2);

        // Test 6: Search in subfolder
        var searchResults = await _libraryService.SearchAllSongIdsInFolderAsync(
            subFolder1.Id, "005", SongSortOrder.TitleAsc);
        searchResults.Should().HaveCount(1);
        searchResults.Should().Contain(song1.Id);
    }
}
