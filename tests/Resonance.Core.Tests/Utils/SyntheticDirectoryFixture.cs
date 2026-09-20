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

    private readonly List<string> _createdLinks = new();

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
                Directory.Delete(linkPath, false);
            }
            Directory.CreateSymbolicLink(linkPath, targetPath);
            _createdLinks.Add(linkPath);
            return true;
        }
        catch
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    proc?.WaitForExit(5000);
                    if (Directory.Exists(linkPath))
                    {
                        _createdLinks.Add(linkPath);
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore fallback creation errors
            }
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            // First unlink all created reparse points so Directory.Delete does not traverse or loop
            foreach (var link in _createdLinks)
            {
                try
                {
                    if (Directory.Exists(link))
                    {
                        Directory.Delete(link, false);
                    }
                }
                catch
                {
                    // Ignore link cleanup errors
                }
            }

            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, true);
            }
        }
        catch
        {
            // Transient temp directory cleanup failure should not fail tests
        }
    }
}
