using FluentAssertions;
using Resonance.Core.Constants;
using Resonance.Core.Helpers;
using Xunit;

namespace Resonance.Core.Tests;

public class FormatCapabilityTests
{
    private static readonly string[] MandatedBaselineFormats =
    {
        ".mp3", ".flac", ".wav", ".aac", ".m4a", ".m4b", ".mp4",
        ".ogg", ".oga", ".opus", ".wma", ".asf", ".aiff",
        ".ape", ".wv", ".dsf", ".dff", ".mpc", ".mpp", ".webm"
    };

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".flac")]
    [InlineData(".wav")]
    [InlineData(".aac")]
    [InlineData(".m4a")]
    [InlineData(".m4b")]
    [InlineData(".mp4")]
    [InlineData(".ogg")]
    [InlineData(".oga")]
    [InlineData(".opus")]
    [InlineData(".wma")]
    [InlineData(".asf")]
    [InlineData(".aiff")]
    [InlineData(".ape")]
    [InlineData(".wv")]
    [InlineData(".dsf")]
    [InlineData(".dff")]
    [InlineData(".mpc")]
    [InlineData(".mpp")]
    [InlineData(".webm")]
    public void MusicFileExtensions_ContainsMandatedBaselineFormat(string extension)
    {
        FileExtensions.MusicFileExtensions.Should().Contain(extension);
        AudioFormatRegistry.IsSupported(extension).Should().BeTrue();
    }

    [Theory]
    [InlineData(".MP3")]
    [InlineData(".FLAC")]
    [InlineData(".Wav")]
    [InlineData(".DFF")]
    [InlineData(".Dsf")]
    [InlineData(".OPUS")]
    [InlineData(".WV")]
    public void MusicFileExtensions_IsCaseInsensitive(string extension)
    {
        FileExtensions.MusicFileExtensions.Contains(extension).Should().BeTrue();
        AudioFormatRegistry.IsSupported(extension).Should().BeTrue();
    }

    [Theory]
    [InlineData(".aax")]
    [InlineData(".m4p")]
    [InlineData(".exe")]
    [InlineData(".txt")]
    [InlineData(".dll")]
    [InlineData(".iso")]
    public void MusicFileExtensions_RejectsUnsupportedAndDrmFormats(string extension)
    {
        FileExtensions.MusicFileExtensions.Should().NotContain(extension);
        AudioFormatRegistry.IsSupported(extension).Should().BeFalse();
    }

    [Theory]
    [InlineData(".opus", true)]
    [InlineData(".ogg", true)]
    [InlineData(".oga", true)]
    [InlineData(".webm", true)]
    [InlineData(".flac", false)]
    [InlineData(".mp3", false)]
    [InlineData(".wav", false)]
    [InlineData(".dsf", false)]
    [InlineData(".dff", false)]
    public void AudioFormatRegistry_DemuxerConfiguration_MatchesPlaybackContract(string extension, bool usesNativeDemuxer)
    {
        AudioFormatRegistry.UsesNativeDemuxer(extension).Should().Be(usesNativeDemuxer);
    }

    [Theory]
    [InlineData(".dsf", "dsf")]
    [InlineData(".dff", "dsf")]
    [InlineData(".wma", "asf")]
    [InlineData(".asf", "asf")]
    [InlineData(".flac", "flac")]
    [InlineData(".mp3", "mp3")]
    [InlineData(".wav", "wav")]
    [InlineData(".aiff", "aiff")]
    [InlineData(".ape", "ape")]
    [InlineData(".wv", "wv")]
    [InlineData(".m4a", "mp4")]
    public void AudioFormatRegistry_LibVlcHint_MatchesPlaybackContract(string extension, string expectedHint)
    {
        AudioFormatRegistry.GetLibVlcHint(extension).Should().Be(expectedHint);
    }

    [Fact]
    public void AllMandatedBaselineFormats_HaveRegisteredCapability()
    {
        foreach (var format in MandatedBaselineFormats)
        {
            var exists = AudioFormatRegistry.TryGetCapability(format, out var capability);
            exists.Should().BeTrue($"Format {format} must have a capability record");
            capability.DisplayName.Should().NotBeNullOrWhiteSpace();
            capability.Extension.Should().Be(format);
        }
    }
}
