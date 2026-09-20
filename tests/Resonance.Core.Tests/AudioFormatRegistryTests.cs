using FluentAssertions;
using Resonance.Core.Constants;
using Resonance.Core.Helpers;
using Xunit;

namespace Resonance.Core.Tests;

public class AudioFormatRegistryTests
{
    [Fact]
    public void Registry_Matches_MusicFileExtensions_Exactly()
    {
        foreach (var ext in FileExtensions.MusicFileExtensions)
        {
            AudioFormatRegistry.IsSupported(ext).Should().BeTrue($"Extension {ext} from MusicFileExtensions should be supported by AudioFormatRegistry");
            AudioFormatRegistry.TryGetCapability(ext, out var capability).Should().BeTrue();
            capability.Extension.Should().BeEquivalentTo(ext);
        }
    }

    [Theory]
    [InlineData(".mp3", true)]
    [InlineData(".flac", true)]
    [InlineData(".wav", true)]
    [InlineData(".aac", true)]
    [InlineData(".m4a", true)]
    [InlineData(".ogg", true)]
    [InlineData(".opus", true)]
    [InlineData(".wma", true)]
    [InlineData(".aiff", true)]
    [InlineData(".ape", true)]
    [InlineData(".wv", true)]
    [InlineData(".dsf", true)]
    [InlineData(".dff", true)]
    [InlineData(".mpc", true)]
    [InlineData(".mpp", true)]
    [InlineData(".xyz", false)]
    [InlineData(".aax", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupported_ReturnsExpected(string? extension, bool expected)
    {
        AudioFormatRegistry.IsSupported(extension).Should().Be(expected);
    }

    [Theory]
    [InlineData(".flac", true)]
    [InlineData(".wav", true)]
    [InlineData(".aiff", true)]
    [InlineData(".ape", true)]
    [InlineData(".wv", true)]
    [InlineData(".dsf", true)]
    [InlineData(".dff", true)]
    [InlineData(".mp3", false)]
    [InlineData(".aac", false)]
    [InlineData(".ogg", false)]
    [InlineData(".opus", false)]
    [InlineData(".wma", false)]
    public void IsLossless_ReturnsExpected(string extension, bool expected)
    {
        AudioFormatRegistry.IsLossless(extension).Should().Be(expected);
    }

    [Theory]
    [InlineData(".dsf", AudioCodecCategory.HiResDsd)]
    [InlineData(".dff", AudioCodecCategory.HiResDsd)]
    [InlineData(".flac", AudioCodecCategory.Lossless)]
    [InlineData(".wav", AudioCodecCategory.Lossless)]
    [InlineData(".mp3", AudioCodecCategory.Lossy)]
    [InlineData(".opus", AudioCodecCategory.Lossy)]
    public void GetCategory_ReturnsExpected(string extension, AudioCodecCategory expected)
    {
        AudioFormatRegistry.GetCategory(extension).Should().Be(expected);
    }

    [Theory]
    [InlineData(".mp3", "mp3")]
    [InlineData(".flac", "flac")]
    [InlineData(".wav", "wav")]
    [InlineData(".dsf", "dsf")]
    [InlineData(".dff", "dsf")]
    [InlineData(".wma", "asf")]
    [InlineData(".m4a", "mp4")]
    public void GetLibVlcHint_ReturnsExpected(string extension, string expected)
    {
        AudioFormatRegistry.GetLibVlcHint(extension).Should().Be(expected);
    }

    [Theory]
    [InlineData(".ogg", true)]
    [InlineData(".oga", true)]
    [InlineData(".opus", true)]
    [InlineData(".webm", true)]
    [InlineData(".mp3", false)]
    [InlineData(".flac", false)]
    public void UsesNativeDemuxer_ReturnsExpected(string extension, bool expected)
    {
        AudioFormatRegistry.UsesNativeDemuxer(extension).Should().Be(expected);
    }

    [Theory]
    [InlineData(".mp3", "MP3")]
    [InlineData(".flac", "FLAC")]
    [InlineData(".dsf", "DSD (DSF)")]
    [InlineData(".dff", "DSD (DFF)")]
    [InlineData(".wv", "WavPack")]
    [InlineData(".ape", "Monkey's Audio")]
    public void GetDisplayName_ReturnsExpected(string extension, string expected)
    {
        AudioFormatRegistry.GetDisplayName(extension).Should().Be(expected);
    }
}
