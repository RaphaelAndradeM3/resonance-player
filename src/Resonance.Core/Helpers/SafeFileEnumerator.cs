using System.IO;

namespace Resonance.Core.Helpers;

/// <summary>
///     Enumerates files without allowing an inaccessible directory, transient filesystem error,
///     or directory reparse-point cycle to abort or hang the whole traversal.
/// </summary>
public static class SafeFileEnumerator
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$RECYCLE.BIN",
        "RECYCLED",
        "RECYCLER",
        "System Volume Information",
        "@eaDir",
        "@Recycle",
        "#recycle",
        ".Trashes",
        ".Trash",
        ".Trash-1000",
        "lost+found",
        ".fseventsd",
        ".Spotlight-V100"
    };

    public static IEnumerable<(string Path, DateTime LastWriteTimeUtc)> EnumerateFilesWithLastWriteTime(
        string rootPath,
        string searchPattern,
        SearchOption searchOption)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) yield break;

        var pending = new Stack<DirectoryInfo>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        DirectoryInfo rootDir;
        try
        {
            rootDir = new DirectoryInfo(rootPath);
            if (!rootDir.Exists) yield break;
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            yield break;
        }

        pending.Push(rootDir);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            var canonicalPath = TryResolveCanonicalPath(directory);
            if (canonicalPath is null) continue;

            string traversalPath;
            try
            {
                traversalPath = Path.GetFullPath(directory.FullName)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception ex) when (IsRecoverableFileSystemException(ex))
            {
                continue;
            }

            // Guard against cycles: verify both traversal path and canonical target path
            if (!visited.Add(traversalPath)) continue;

            if (!string.Equals(canonicalPath, traversalPath, StringComparison.OrdinalIgnoreCase))
            {
                if (!visited.Add(canonicalPath)) continue;
            }

            foreach (var file in EnumerateFilesSafely(directory, searchPattern))
            {
                string path;
                DateTime lastWriteTimeUtc;
                try
                {
                    path = file.FullName;
                    lastWriteTimeUtc = file.LastWriteTimeUtc;
                }
                catch (Exception ex) when (IsRecoverableFileSystemException(ex))
                {
                    continue;
                }

                yield return (path, lastWriteTimeUtc);
            }

            if (searchOption == SearchOption.TopDirectoryOnly) continue;

            foreach (var child in EnumerateDirectoriesSafely(directory))
            {
                if (IsExcludedDirectory(child)) continue;
                pending.Push(child);
            }
        }
    }

    internal static bool IsExcludedDirectory(DirectoryInfo directory)
    {
        if (ExcludedDirectoryNames.Contains(directory.Name)) return true;
        if (directory.Name.Length > 0 && directory.Name[0] == '$') return true;

        try
        {
            return IsExcludedAttributes(directory.Attributes);
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            // If a child cannot be classified safely, skip it for this scan.
            return true;
        }
    }

    internal static bool IsExcludedAttributes(FileAttributes attributes)
    {
        // Following junctions/symbolic links is supported with cycle protection via canonical path tracking.
        // Hidden alone is allowed for user-hidden music. Hidden + System is a strong OS-directory signal.
        return (attributes & (FileAttributes.Hidden | FileAttributes.System))
               == (FileAttributes.Hidden | FileAttributes.System);
    }

    /// <summary>
    ///     Resolves the real canonical physical target of a directory, following junctions
    ///     or symbolic links if applicable. Returns null if invalid or inaccessible.
    /// </summary>
    public static string? TryResolveCanonicalPath(DirectoryInfo directory)
    {
        try
        {
            if (!directory.Exists) return null;

            FileSystemInfo target = directory;
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                var resolved = directory.ResolveLinkTarget(returnFinalTarget: true);
                if (resolved is not null)
                {
                    target = resolved;
                }
            }

            if (!Directory.Exists(target.FullName)) return null;

            return Path.GetFullPath(target.FullName)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            return null;
        }
    }

    private static IEnumerable<FileInfo> EnumerateFilesSafely(DirectoryInfo directory, string searchPattern)
    {
        IEnumerator<FileInfo>? enumerator = null;
        try
        {
            enumerator = directory.EnumerateFiles(searchPattern).GetEnumerator();
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            // Handled below by returning an empty sequence.
        }

        if (enumerator is null) yield break;

        using (enumerator)
        {
            while (true)
            {
                var shouldStop = false;
                var hasNext = false;
                try
                {
                    hasNext = enumerator.MoveNext();
                }
                catch (Exception ex) when (IsRecoverableFileSystemException(ex))
                {
                    // An iterator that throws is not guaranteed to advance. Stop this directory
                    // instead of retrying the same failing MoveNext forever.
                    shouldStop = true;
                }

                if (shouldStop || !hasNext) yield break;
                if (enumerator.Current is { } file) yield return file;
            }
        }
    }

    private static IEnumerable<DirectoryInfo> EnumerateDirectoriesSafely(DirectoryInfo directory)
    {
        IEnumerator<DirectoryInfo>? enumerator = null;
        try
        {
            enumerator = directory.EnumerateDirectories().GetEnumerator();
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            // Handled below by returning an empty sequence.
        }

        if (enumerator is null) yield break;

        using (enumerator)
        {
            while (true)
            {
                var shouldStop = false;
                var hasNext = false;
                try
                {
                    hasNext = enumerator.MoveNext();
                }
                catch (Exception ex) when (IsRecoverableFileSystemException(ex))
                {
                    shouldStop = true;
                }

                if (shouldStop || !hasNext) yield break;
                if (enumerator.Current is { } child) yield return child;
            }
        }
    }

    internal static bool IsRecoverableFileSystemException(Exception exception) =>
        exception is UnauthorizedAccessException
            or DirectoryNotFoundException
            or FileNotFoundException
            or PathTooLongException
            or IOException
            or System.Security.SecurityException
            or ArgumentException
            or NotSupportedException;
}
