using System.IO;

namespace Resonance.Core.Tests.Utils;

/// <summary>
///     Provides isolated temporary file system hierarchies for testing recursive
///     traversal, long paths, junctions, and edge cases. Cleans up upon disposal.
/// </summary>
public sealed class SyntheticDirectoryFixture : IDisposable
{
    public string RootPath { get; }

    public SyntheticDirectoryFixture(string? prefix = null)
    {
        var dirName = $"Resonance_Synth_{prefix ?? "Test"}_{Guid.NewGuid():N}";
        RootPath = Path.Combine(Path.GetTempPath(), dirName);
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>
    ///     Creates a nested subdirectory path of specified depth under the fixture root.
    /// </summary>
    public string CreateNestedDirectory(int depth, string? baseDir = null)
    {
        var current = baseDir ?? RootPath;
        for (var i = 1; i <= depth; i++)
        {
            current = Path.Combine(current, $"Level_{i}");
        }
        Directory.CreateDirectory(current);
        return current;
    }

    /// <summary>
    ///     Creates a mock audio file at the given directory with dummy content.
    /// </summary>
    public string CreateMockAudioFile(string directory, string fileName = "test.mp3", byte[]? content = null)
    {
        Directory.CreateDirectory(directory);
        var fullPath = Path.Combine(directory, fileName);
        File.WriteAllBytes(fullPath, content ?? [0xFF, 0xFB, 0x90, 0x44]); // Minimal MP3 sync frame dummy
        return fullPath;
    }

    /// <summary>
    ///     Attempts to create a directory symbolic link or junction on Windows.
    ///     Returns true if successfully created; false if insufficient privileges or unsupported.
    /// </summary>
    public bool TryCreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            if (Directory.Exists(linkPath))
            {
                Directory.Delete(linkPath, true);
            }
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                // Remove readonly attributes if any before deleting
                foreach (var file in Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch
                    {
                        // Ignore individual cleanup errors
                    }
                }
                Directory.Delete(RootPath, true);
            }
        }
        catch
        {
            // Transient temp directory cleanup failure should not fail tests
        }
    }
}
