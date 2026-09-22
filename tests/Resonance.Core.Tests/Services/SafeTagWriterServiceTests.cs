using ATL;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;
using Resonance.Core.Services.Implementations;
using Xunit;

namespace Resonance.Core.Tests.Services;

/// <summary>
///     Unit tests for <see cref="SafeTagWriterService"/> verifying atomic writing,
///     audio container integrity validation, read-only attribute handling,
///     playback interruption coordination, and database synchronization.
/// </summary>
public class SafeTagWriterServiceTests : IDisposable
{
    private readonly IFileSystemService _fileSystem;
    private readonly IMetadataService _metadataService;
    private readonly ILibraryWriter _libraryWriter;
    private readonly ILibraryReader _libraryReader;
    private readonly IMusicPlaybackService _playbackService;
    private readonly ILogger<SafeTagWriterService> _logger;
    private readonly string _tempDirectory;

    private static readonly byte[] ValidMp3Bytes = CreateValidMp3Bytes();

    public SafeTagWriterServiceTests()
    {
        _fileSystem = Substitute.For<IFileSystemService>();
        _metadataService = Substitute.For<IMetadataService>();
        _libraryWriter = Substitute.For<ILibraryWriter>();
        _libraryReader = Substitute.For<ILibraryReader>();
        _playbackService = Substitute.For<IMusicPlaybackService>();
        _logger = Substitute.For<ILogger<SafeTagWriterService>>();

        _tempDirectory = Path.Combine(Path.GetTempPath(), $"resonance_tagwriter_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        _fileSystem.FileExists(Arg.Any<string>()).Returns(ci => File.Exists(ci.ArgAt<string>(0)));
        _fileSystem.GetDirectoryName(Arg.Any<string>()).Returns(ci => Path.GetDirectoryName(ci.ArgAt<string>(0)));
        _fileSystem.GetFileName(Arg.Any<string>()).Returns(ci => Path.GetFileName(ci.ArgAt<string>(0)));
        _fileSystem.When(fs => fs.CopyFile(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Do(ci => File.Copy(ci.ArgAt<string>(0), ci.ArgAt<string>(1), ci.ArgAt<bool>(2)));
    }

    [Fact]
    public async Task ApplyWritePlanAsync_WithValidPlan_AppliesChangesAtomicallyAndSyncsLibrary()
    {
        // Arrange
        var filePath = CreateTestMp3File("test1.mp3", track =>
        {
            track.Title = "Old Title";
            track.Artist = "Old Artist";
            track.Album = "Old Album";
            track.Year = 2010;
        });

        var songId = Guid.NewGuid();
        var existingSong = new Song
        {
            Id = songId,
            Title = "Old Title",
            ArtistName = "Old Artist",
            FilePath = filePath,
            DirectoryPath = _tempDirectory
        };

        _libraryReader.GetSongByIdAsync(songId).Returns(existingSong);
        _metadataService.ExtractMetadataAsync(filePath, includeMediaAssets: true).Returns(new SongFileMetadata
        {
            FilePath = filePath,
            Title = "New Title",
            Artists = new List<string> { "New Artist" },
            Album = "New Album",
            Year = 2024,
            TrackNumber = 3,
            DiscNumber = 1
        });

        var service = new SafeTagWriterService(
            _fileSystem,
            _metadataService,
            _libraryWriter,
            _logger,
            _libraryReader,
            _playbackService);

        var plan = new TagWritePlan
        {
            FilePath = filePath,
            SongId = songId,
            SelectedChanges = new List<TagDiffRecord>
            {
                new()
                {
                    FieldKey = "Title",
                    DisplayName = "Título",
                    OriginalValue = "Old Title",
                    ProposedValue = "New Title",
                    IsSelected = true
                },
                new()
                {
                    FieldKey = "Artist",
                    DisplayName = "Artista",
                    OriginalValue = "Old Artist",
                    ProposedValue = "New Artist",
                    IsSelected = true
                },
                new()
                {
                    FieldKey = "Album",
                    DisplayName = "Álbum",
                    OriginalValue = "Old Album",
                    ProposedValue = "New Album",
                    IsSelected = true
                },
                new()
                {
                    FieldKey = "Year",
                    DisplayName = "Ano",
                    OriginalValue = "2010",
                    ProposedValue = "2024",
                    IsSelected = true
                }
            }
        };

        // Act
        var result = await service.ApplyWritePlanAsync(plan);

        // Assert
        result.Success.Should().BeTrue();
        result.FieldsUpdatedCount.Should().Be(4);
        result.WasPlaybackInterrupted.Should().BeFalse();

        // Verify tags on disk via ATL
        var verifiedTrack = new Track(filePath);
        verifiedTrack.Title.Should().Be("New Title");
        verifiedTrack.Artist.Should().Be("New Artist");
        verifiedTrack.Album.Should().Be("New Album");
        verifiedTrack.Year.Should().Be(2024);

        // Verify SQLite database update was triggered
        await _libraryWriter.Received(1).UpdateSongAsync(Arg.Is<Song>(s =>
            s.Id == songId &&
            s.Title == "New Title" &&
            s.ArtistName == "New Artist" &&
            s.Year == 2024));
    }

    [Fact]
    public async Task ApplyWritePlanAsync_WithNewPicture_EmbedsCoverArt()
    {
        // Arrange
        var filePath = CreateTestMp3File("test_cover.mp3", track =>
        {
            track.Title = "Cover Test";
        });

        var samplePngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };

        var service = new SafeTagWriterService(
            _fileSystem,
            _metadataService,
            _libraryWriter,
            _logger,
            _libraryReader,
            _playbackService);

        var plan = new TagWritePlan
        {
            FilePath = filePath,
            NewPictureBytes = samplePngBytes,
            PictureMimeType = "image/png",
            SelectedChanges = new List<TagDiffRecord>()
        };

        // Act
        var result = await service.ApplyWritePlanAsync(plan);

        // Assert
        result.Success.Should().BeTrue();
        result.FieldsUpdatedCount.Should().Be(1);

        var updatedTrack = new Track(filePath);
        updatedTrack.EmbeddedPictures.Should().NotBeEmpty();
        updatedTrack.EmbeddedPictures[0].PicType.Should().Be(PictureInfo.PIC_TYPE.Front);
    }

    [Fact]
    public async Task ApplyWritePlanAsync_WithRemovePicture_ClearsCoverArt()
    {
        // Arrange
        var samplePngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
        var filePath = CreateTestMp3File("test_remove_cover.mp3", track =>
        {
            track.Title = "With Cover";
            track.EmbeddedPictures.Add(PictureInfo.fromBinaryData(samplePngBytes));
        });

        var service = new SafeTagWriterService(
            _fileSystem,
            _metadataService,
            _libraryWriter,
            _logger,
            _libraryReader,
            _playbackService);

        var plan = new TagWritePlan
        {
            FilePath = filePath,
            RemovePicture = true,
            SelectedChanges = new List<TagDiffRecord>()
        };

        // Act
        var result = await service.ApplyWritePlanAsync(plan);

        // Assert
        result.Success.Should().BeTrue();
        result.FieldsUpdatedCount.Should().Be(1);

        var updatedTrack = new Track(filePath);
        updatedTrack.EmbeddedPictures.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyWritePlanAsync_WhenFileIsReadOnly_RemovesFlagAndSucceeds()
    {
        // Arrange
        var filePath = CreateTestMp3File("readonly.mp3", track =>
        {
            track.Title = "Read Only Title";
        });

        File.SetAttributes(filePath, FileAttributes.ReadOnly);

        var service = new SafeTagWriterService(
            _fileSystem,
            _metadataService,
            _libraryWriter,
            _logger,
            _libraryReader,
            _playbackService);

        var plan = new TagWritePlan
        {
            FilePath = filePath,
            SelectedChanges = new List<TagDiffRecord>
            {
                new()
                {
                    FieldKey = "Title",
                    DisplayName = "Título",
                    OriginalValue = "Read Only Title",
                    ProposedValue = "Writable Title",
                    IsSelected = true
                }
            }
        };

        // Act
        var result = await service.ApplyWritePlanAsync(plan);

        // Assert
        result.Success.Should().BeTrue();
        var updatedTrack = new Track(filePath);
        updatedTrack.Title.Should().Be("Writable Title");

        // Clean up readonly attribute if needed
        var attrs = File.GetAttributes(filePath);
        if (attrs.HasFlag(FileAttributes.ReadOnly))
        {
            File.SetAttributes(filePath, attrs & ~FileAttributes.ReadOnly);
        }
    }

    [Fact]
    public async Task ApplyWritePlanAsync_WhenTrackIsPlaying_CoordinatesPauseAndResume()
    {
        // Arrange
        var filePath = CreateTestMp3File("playing.mp3", track =>
        {
            track.Title = "Playing Title";
        });

        var playingSong = new Song
        {
            Id = Guid.NewGuid(),
            FilePath = filePath,
            Title = "Playing Title"
        };

        _playbackService.CurrentTrack.Returns(playingSong);
        _playbackService.IsPlaying.Returns(true);
        _playbackService.CurrentPosition.Returns(TimeSpan.FromSeconds(35));

        // When PlayPauseAsync is called to pause, set IsPlaying to false
        _playbackService.When(p => p.PlayPauseAsync()).Do(_ =>
        {
            _playbackService.IsPlaying.Returns(!_playbackService.IsPlaying);
        });

        var service = new SafeTagWriterService(
            _fileSystem,
            _metadataService,
            _libraryWriter,
            _logger,
            _libraryReader,
            _playbackService);

        var plan = new TagWritePlan
        {
            FilePath = filePath,
            SelectedChanges = new List<TagDiffRecord>
            {
                new()
                {
                    FieldKey = "Title",
                    DisplayName = "Título",
                    OriginalValue = "Playing Title",
                    ProposedValue = "Updated Playing Title",
                    IsSelected = true
                }
            }
        };

        // Act
        var result = await service.ApplyWritePlanAsync(plan);

        // Assert
        result.Success.Should().BeTrue();
        result.WasPlaybackInterrupted.Should().BeTrue();

        // Paused, seeked, resumed
        await _playbackService.Received().SeekAsync(TimeSpan.FromSeconds(35));
        await _playbackService.Received(2).PlayPauseAsync(); // 1st pause, 2nd resume
    }

    [Fact]
    public async Task ApplyWritePlanAsync_WhenFileDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "non_existent.mp3");
        var service = new SafeTagWriterService(
            _fileSystem,
            _metadataService,
            _libraryWriter,
            _logger,
            _libraryReader,
            _playbackService);

        var plan = new TagWritePlan
        {
            FilePath = nonExistentPath,
            SelectedChanges = new List<TagDiffRecord>()
        };

        // Act
        var result = await service.ApplyWritePlanAsync(plan);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("não foi encontrado");
    }

    [Fact]
    public async Task ValidateAudioFileIntegrityAsync_WithValidMp3_ReturnsTrue()
    {
        // Arrange
        var filePath = CreateTestMp3File("valid_int.mp3", track => track.Title = "Integrity");
        var service = new SafeTagWriterService(
            _fileSystem,
            _metadataService,
            _libraryWriter,
            _logger);

        // Act
        var isValid = await service.ValidateAudioFileIntegrityAsync(filePath);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAudioFileIntegrityAsync_WithCorruptOrNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistent = Path.Combine(_tempDirectory, "ghost.mp3");
        var corruptFile = Path.Combine(_tempDirectory, "corrupt.mp3");
        File.WriteAllBytes(corruptFile, new byte[] { 0x00, 0x01, 0x02 });

        var service = new SafeTagWriterService(
            _fileSystem,
            _metadataService,
            _libraryWriter,
            _logger);

        // Act & Assert
        (await service.ValidateAudioFileIntegrityAsync(nonExistent)).Should().BeFalse();
        (await service.ValidateAudioFileIntegrityAsync(corruptFile)).Should().BeFalse();
    }

    private string CreateTestMp3File(string fileName, Action<Track> trackSetup)
    {
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllBytes(filePath, ValidMp3Bytes);

        var track = new Track(filePath);
        trackSetup(track);
        track.Save();

        return filePath;
    }

    private static byte[] CreateValidMp3Bytes()
    {
        using var ms = new MemoryStream();

        // ID3v2.3 header (10 bytes)
        ms.Write(new byte[] { 0x49, 0x44, 0x33 }); // "ID3"
        ms.Write(new byte[] { 0x03, 0x00 });       // Version 2.3
        ms.Write(new byte[] { 0x00 });             // Flags
        ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // Size

        // Add 10 minimal MP3 audio frames (MPEG1 Layer3, 128kbps, 44100Hz)
        for (var i = 0; i < 10; i++)
        {
            ms.Write(new byte[] { 0xFF, 0xFB, 0x90, 0x00 });
            ms.Write(new byte[413]);
        }

        return ms.ToArray();
    }

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                // Remove readonly flags before deleting
                foreach (var file in Directory.GetFiles(_tempDirectory, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch
                    {
                        // Ignore
                    }
                }
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }

        GC.SuppressFinalize(this);
    }
}
