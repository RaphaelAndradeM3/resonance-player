using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Resonance.Core.Helpers;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;
using Resonance.Core.Services.Implementations;
using Resonance.Core.Tests.Utils;
using Xunit;

namespace Resonance.Core.Tests;

public class FormatResilienceTests : IDisposable
{
    private readonly AudioFormatTestFixture _fixture;
    private readonly IFileSystemService _fileSystem;
    private readonly IImageProcessor _imageProcessor;
    private readonly IPathConfiguration _pathConfig;
    private readonly ILogger<AtlMetadataService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly AtlMetadataService _metadataService;

    public FormatResilienceTests()
    {
        _fixture = new AudioFormatTestFixture("Resilience");
        _fileSystem = Substitute.For<IFileSystemService>();
        _imageProcessor = Substitute.For<IImageProcessor>();
        _pathConfig = Substitute.For<IPathConfiguration>();
        _logger = Substitute.For<ILogger<AtlMetadataService>>();
        _settingsService = Substitute.For<ISettingsService>();

        _pathConfig.LrcCachePath.Returns(Path.Combine(_fixture.RootPath, "lrc_cache"));

        _fileSystem.GetFileNameWithoutExtension(Arg.Any<string>())
            .Returns(callInfo => Path.GetFileNameWithoutExtension(callInfo.ArgAt<string>(0)) ?? string.Empty);
        _fileSystem.GetDirectoryName(Arg.Any<string>())
            .Returns(callInfo => Path.GetDirectoryName(callInfo.ArgAt<string>(0)));
        _fileSystem.Combine(Arg.Any<string[]>())
            .Returns(callInfo => Path.Combine(callInfo.ArgAt<string[]>(0)));
        _fileSystem.GetExtension(Arg.Any<string>())
            .Returns(callInfo => Path.GetExtension(callInfo.ArgAt<string>(0)));
        _fileSystem.GetFileInfo(Arg.Any<string>())
            .Returns(callInfo => new FileInfo(callInfo.ArgAt<string>(0)));
        _fileSystem.FileExists(Arg.Any<string>())
            .Returns(callInfo => File.Exists(callInfo.ArgAt<string>(0)));

        _settingsService.GetArtistSplitCharactersAsync().Returns(Task.FromResult(string.Empty));
        _settingsService.GetGenreSplitCharactersAsync().Returns(Task.FromResult(string.Empty));

        _metadataService = new AtlMetadataService(_imageProcessor, _fileSystem, _pathConfig, _logger, _settingsService);
    }

    [Theory]
    [InlineData("empty.mp3")]
    [InlineData("empty.flac")]
    [InlineData("empty.wav")]
    [InlineData("empty.dsf")]
    public async Task ExtractMetadataAsync_ZeroByteFile_FlagsEmptyFileWithoutThrowing(string fileName)
    {
        var filePath = _fixture.CreateEmptyFile(_fixture.RootPath, fileName);

        var result = await _metadataService.ExtractMetadataAsync(filePath);

        result.Should().NotBeNull();
        result.ExtractionFailed.Should().BeTrue();
        result.ErrorMessage.Should().Be("EmptyFile");
    }

    [Theory]
    [InlineData("truncated.flac", 8)]
    [InlineData("truncated.mp3", 16)]
    [InlineData("truncated.wav", 12)]
    public async Task ExtractMetadataAsync_TruncatedCorruptHeader_FlagsCorruptOrUnsupported(string fileName, int byteCount)
    {
        var filePath = _fixture.CreateTruncatedFile(_fixture.RootPath, fileName, byteCount);

        var result = await _metadataService.ExtractMetadataAsync(filePath);

        result.Should().NotBeNull();
        result.ExtractionFailed.Should().BeTrue();
        result.ErrorMessage.Should().BeOneOf("CorruptFile", "UnsupportedFormat");
    }

    [Fact]
    public async Task ExtractMetadataAsync_FakeExtensionWithPlainText_FlagsUnsupportedOrCorrupt()
    {
        var filePath = Path.Combine(_fixture.RootPath, "fake_audio.flac");
        await File.WriteAllTextAsync(filePath, "This is plain text pretending to be a FLAC audio file.");

        var result = await _metadataService.ExtractMetadataAsync(filePath);

        result.Should().NotBeNull();
        result.ExtractionFailed.Should().BeTrue();
        result.ErrorMessage.Should().BeOneOf("UnsupportedFormat", "CorruptFile");
    }

    [Fact]
    public async Task ExtractMetadataAsync_ValidSynthesizedWav_ExtractsSuccessfully()
    {
        var filePath = _fixture.CreateValidWavFile(_fixture.RootPath, "valid.wav", durationSeconds: 2, sampleRate: 44100, channels: 2);

        var result = await _metadataService.ExtractMetadataAsync(filePath);

        result.Should().NotBeNull();
        result.ExtractionFailed.Should().BeFalse();
        result.Duration.TotalSeconds.Should().BeInRange(1.8, 2.2);
        result.SampleRate.Should().Be(44100);
        result.Channels.Should().Be(2);
    }

    public void Dispose()
    {
        _metadataService.Dispose();
        _fixture.Dispose();
    }
}
