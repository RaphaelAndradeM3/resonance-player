using System.IO;
using System.Text;

namespace Resonance.Core.Tests.Utils;

/// <summary>
///     Provides isolated temporary file system environments for testing multi-format audio files,
///     valid PCM streams, empty (0-byte) files, and corrupted headers.
/// </summary>
public sealed class AudioFormatTestFixture : IDisposable
{
    public string RootPath { get; }

    public AudioFormatTestFixture(string? prefix = null)
    {
        var dirName = $"Resonance_AudioFormat_{prefix ?? "Test"}_{Guid.NewGuid():N}";
        RootPath = Path.Combine(Path.GetTempPath(), dirName);
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>
    ///     Creates a zero-byte empty file with the given name under the specified directory.
    /// </summary>
    public string CreateEmptyFile(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, Array.Empty<byte>());
        return path;
    }

    /// <summary>
    ///     Creates a corrupt/truncated file with a few random bytes that do not form a valid audio stream.
    /// </summary>
    public string CreateTruncatedFile(string directory, string fileName, int byteCount = 8)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        var bytes = new byte[byteCount];
        for (var i = 0; i < byteCount; i++)
        {
            bytes[i] = (byte)(0x20 + i);
        }
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    ///     Creates a synthetic valid PCM WAV file with proper RIFF, fmt, and data chunks.
    /// </summary>
    public string CreateValidWavFile(string directory, string fileName, int durationSeconds = 1, int sampleRate = 44100, short channels = 2)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);

        const short bitsPerSample = 16;
        var byteRate = sampleRate * channels * (bitsPerSample / 8);
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var subChunk2Size = durationSeconds * byteRate;
        var chunkSize = 36 + subChunk2Size;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        // RIFF chunk descriptor
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(chunkSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        // "fmt " sub-chunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // Subchunk1Size for PCM
        writer.Write((short)1); // AudioFormat: 1 = PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);

        // "data" sub-chunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(subChunk2Size);

        // Write silent PCM samples
        var zeroBuffer = new byte[1024];
        var bytesRemaining = subChunk2Size;
        while (bytesRemaining > 0)
        {
            var toWrite = Math.Min(bytesRemaining, zeroBuffer.Length);
            writer.Write(zeroBuffer, 0, toWrite);
            bytesRemaining -= toWrite;
        }

        return path;
    }

    /// <summary>
    ///     Creates an arbitrary file with the provided byte content.
    /// </summary>
    public string CreateMockFile(string directory, string fileName, byte[] content)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, true);
            }
        }
        catch
        {
            // Best effort cleanup of temp files
        }
    }
}
