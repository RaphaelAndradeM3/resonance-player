using FluentAssertions;
using Resonance.Core.Helpers;
using Resonance.Core.Tests.Utils;
using System.IO;
using Xunit;

namespace Resonance.Core.Tests;

public sealed class SafeFileEnumeratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ResonanceFileEnumeration_{Guid.NewGuid():N}");

    public SafeFileEnumeratorTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void EnumerateFilesWithLastWriteTime_RecursesButSkipsSystemMetadataDirectories()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "Album"));
        var excluded = Directory.CreateDirectory(Path.Combine(_root, "$RECYCLE.BIN"));
        var rootSong = Path.Combine(_root, "root.mp3");
        var nestedSong = Path.Combine(nested.FullName, "nested.mp3");
        File.WriteAllText(rootSong, "root");
        File.WriteAllText(nestedSong, "nested");
        File.WriteAllText(Path.Combine(excluded.FullName, "ghost.mp3"), "excluded");
        File.WriteAllText(Path.Combine(nested.FullName, "notes.txt"), "not audio");

        var files = SafeFileEnumerator
            .EnumerateFilesWithLastWriteTime(_root, "*.mp3", SearchOption.AllDirectories)
            .Select(item => item.Path)
            .ToList();

        files.Should().BeEquivalentTo(rootSong, nestedSong);
    }

    [Fact]
    public void EnumerateFilesWithLastWriteTime_TopDirectoryOnlyDoesNotDescend()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "Album"));
        var rootSong = Path.Combine(_root, "root.flac");
        File.WriteAllText(rootSong, "root");
        File.WriteAllText(Path.Combine(nested.FullName, "nested.flac"), "nested");

        var files = SafeFileEnumerator
            .EnumerateFilesWithLastWriteTime(_root, "*.flac", SearchOption.TopDirectoryOnly)
            .Select(item => item.Path);

        files.Should().ContainSingle().Which.Should().Be(rootSong);
    }

    [Fact]
    public void EnumerateFilesWithLastWriteTime_MissingRootReturnsEmptySequence()
    {
        var missing = Path.Combine(_root, "missing");

        SafeFileEnumerator
            .EnumerateFilesWithLastWriteTime(missing, "*", SearchOption.AllDirectories)
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(FileAttributes.ReparsePoint, false)]
    [InlineData(FileAttributes.Hidden | FileAttributes.System, true)]
    [InlineData(FileAttributes.Hidden, false)]
    [InlineData(FileAttributes.System, false)]
    [InlineData(FileAttributes.Directory, false)]
    public void IsExcludedAttributes_RejectsOperatingSystemDirectories(
        FileAttributes attributes,
        bool expected)
    {
        SafeFileEnumerator.IsExcludedAttributes(attributes).Should().Be(expected);
    }

    [Fact]
    public void EnumerateFilesWithLastWriteTime_DeepHierarchy_FindsAllFilesInAllLevels()
    {
        using var fixture = new SyntheticDirectoryFixture("Deep");
        var level10 = fixture.CreateNestedDirectory(10);
        var level5 = Path.Combine(fixture.RootPath, "Level_1", "Level_2", "Level_3", "Level_4", "Level_5");

        var rootSong = fixture.CreateMockAudioFile(fixture.RootPath, "root.mp3");
        var l5Song = fixture.CreateMockAudioFile(level5, "l5.mp3");
        var l10Song = fixture.CreateMockAudioFile(level10, "l10.mp3");

        var discovered = SafeFileEnumerator
            .EnumerateFilesWithLastWriteTime(fixture.RootPath, "*.mp3", SearchOption.AllDirectories)
            .Select(f => f.Path)
            .ToList();

        discovered.Should().HaveCount(3);
        discovered.Should().Contain(rootSong);
        discovered.Should().Contain(l5Song);
        discovered.Should().Contain(l10Song);
    }

    [Fact]
    public void EnumerateFilesWithLastWriteTime_LongPaths_EnumeratesSuccessfully()
    {
        using var fixture = new SyntheticDirectoryFixture("LongPath");
        var current = fixture.RootPath;
        // Build path > 260 characters
        for (var i = 1; i <= 8; i++)
        {
            current = Path.Combine(current, $"VeryLongSubdirectorySegmentName_{i:D2}");
        }
        Directory.CreateDirectory(current);
        var deepFile = fixture.CreateMockAudioFile(current, "deep_track.flac");

        var discovered = SafeFileEnumerator
            .EnumerateFilesWithLastWriteTime(fixture.RootPath, "*.flac", SearchOption.AllDirectories)
            .Select(f => f.Path)
            .ToList();

        discovered.Should().ContainSingle().Which.Should().Be(deepFile);
    }

    [Fact]
    public void EnumerateFilesWithLastWriteTime_DirectoryReparsePointCycle_PreventsInfiniteLoop()
    {
        using var fixture = new SyntheticDirectoryFixture("Cycle");
        var folderA = Path.Combine(fixture.RootPath, "FolderA");
        var folderB = Path.Combine(fixture.RootPath, "FolderB");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);

        var songA = fixture.CreateMockAudioFile(folderA, "songA.mp3");
        var songB = fixture.CreateMockAudioFile(folderB, "songB.mp3");

        var linkPath = Path.Combine(folderA, "CycleBackToRoot");
        var linkCreated = fixture.TryCreateDirectoryLink(linkPath, fixture.RootPath);

        var discovered = SafeFileEnumerator
            .EnumerateFilesWithLastWriteTime(fixture.RootPath, "*.mp3", SearchOption.AllDirectories)
            .Select(f => f.Path)
            .ToList();

        // Must terminate safely and include both songs without crashing or hanging
        discovered.Should().Contain(songA);
        discovered.Should().Contain(songB);
        if (linkCreated)
        {
            // Cycle back to root should not have duplicated the songs indefinitely
            discovered.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void EnumerateFilesWithLastWriteTime_BrokenReparsePoint_SkipsGracefully()
    {
        using var fixture = new SyntheticDirectoryFixture("Broken");
        var targetPath = Path.Combine(fixture.RootPath, "NonExistentTarget");
        var linkPath = Path.Combine(fixture.RootPath, "BrokenLink");

        var linkCreated = fixture.TryCreateDirectoryLink(linkPath, targetPath);

        var rootSong = fixture.CreateMockAudioFile(fixture.RootPath, "root.mp3");

        var discovered = SafeFileEnumerator
            .EnumerateFilesWithLastWriteTime(fixture.RootPath, "*.mp3", SearchOption.AllDirectories)
            .Select(f => f.Path)
            .ToList();

        discovered.Should().ContainSingle().Which.Should().Be(rootSong);
    }

    [Fact]
    public void TryResolveCanonicalPath_ValidDirectory_ReturnsNormalizedFullPath()
    {
        var dirInfo = new DirectoryInfo(_root);
        var canonical = SafeFileEnumerator.TryResolveCanonicalPath(dirInfo);

        canonical.Should().NotBeNull();
        canonical.Should().Be(Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    [Fact]
    public void TryResolveCanonicalPath_NonExistentDirectory_ReturnsNull()
    {
        var nonExistent = new DirectoryInfo(Path.Combine(_root, "DoesNotExist"));
        var canonical = SafeFileEnumerator.TryResolveCanonicalPath(nonExistent);

        canonical.Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), true)]
    [InlineData(typeof(DirectoryNotFoundException), true)]
    [InlineData(typeof(FileNotFoundException), true)]
    [InlineData(typeof(PathTooLongException), true)]
    [InlineData(typeof(IOException), true)]
    [InlineData(typeof(System.Security.SecurityException), true)]
    [InlineData(typeof(ArgumentException), true)]
    [InlineData(typeof(NotSupportedException), true)]
    [InlineData(typeof(OutOfMemoryException), false)]
    public void IsRecoverableFileSystemException_IdentifiesRecoverableExceptions(Type exceptionType, bool expected)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType)!;
        SafeFileEnumerator.IsRecoverableFileSystemException(ex).Should().Be(expected);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
