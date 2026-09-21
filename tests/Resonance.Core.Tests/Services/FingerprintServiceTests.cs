namespace Resonance.Core.Tests.Services;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Resonance.Core.Services.Implementations;
using Xunit;

public class FingerprintServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly FFmpegFingerprintService _service;

    public FingerprintServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ResonanceFingerprintTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _service = new FFmpegFingerprintService(NullLogger<FFmpegFingerprintService>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignora falhas de cleanup em testes
        }
    }

    [Fact]
    public async Task GenerateFingerprintAsync_WithValidSyntheticAudio_ReturnsValidFingerprint()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "sine_15s.wav");
        CreateWavFile(filePath, durationSeconds: 15, frequency: 440.0);

        // Act
        var result = await _service.GenerateFingerprintAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
        result.Value.Hash.Should().NotBeNullOrWhiteSpace();
        result.Value.DurationSeconds.Should().Be(15);
        result.Value.Algorithm.Should().Be(1);
    }

    [Fact]
    public async Task GenerateFingerprintAsync_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var invalidPath = Path.Combine(_tempDirectory, "non_existent.wav");

        // Act
        var result = await _service.GenerateFingerprintAsync(invalidPath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateFingerprintAsync_WithShortAudio_ReturnsFingerprintWithShortDuration()
    {
        // Arrange: áudio de 5 segundos (< 10 segundos)
        var filePath = Path.Combine(_tempDirectory, "short_5s.wav");
        CreateWavFile(filePath, durationSeconds: 5, frequency: 880.0);

        // Act
        var result = await _service.GenerateFingerprintAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
        result.Value.DurationSeconds.Should().Be(5);
        result.Value.DurationSeconds.Should().BeLessThan(10);
    }

    [Fact]
    public async Task GenerateFingerprintAsync_WithSilentAudio_GeneratesControlledFingerprint()
    {
        // Arrange: áudio com silêncio absoluto de 15 segundos
        var filePath = Path.Combine(_tempDirectory, "silent_15s.wav");
        CreateWavFile(filePath, durationSeconds: 15, frequency: 0.0);

        // Act
        var result = await _service.GenerateFingerprintAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
        result.Value.DurationSeconds.Should().Be(15);
    }

    [Fact]
    public async Task GenerateFingerprintAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "cancel_test.wav");
        CreateWavFile(filePath, durationSeconds: 15, frequency: 440.0);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _service.GenerateFingerprintAsync(filePath, cts.Token);
        });
    }

    private static void CreateWavFile(string filePath, int durationSeconds, double frequency)
    {
        const int sampleRate = 44100;
        const short channels = 1;
        const short bitsPerSample = 16;
        int totalSamples = sampleRate * durationSeconds;
        int dataBytes = totalSamples * (bitsPerSample / 8);

        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);

        // Header RIFF
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8.ToArray());

        // fmt chunk
        writer.Write("fmt "u8.ToArray());
        writer.Write(16); // subchunk size
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);

        // data chunk
        writer.Write("data"u8.ToArray());
        writer.Write(dataBytes);

        for (int i = 0; i < totalSamples; i++)
        {
            short sample = frequency > 0
                ? (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * 16000)
                : (short)0;
            writer.Write(sample);
        }
    }
}
