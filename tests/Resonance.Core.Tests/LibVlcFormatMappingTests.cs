using FluentAssertions;
using Resonance.Core.Constants;
using Resonance.Core.Helpers;
using Xunit;

namespace Resonance.Core.Tests;

public class LibVlcFormatMappingTests
{
    [Theory]
    [InlineData(".opus")]
    [InlineData(".ogg")]
    [InlineData(".oga")]
    [InlineData(".webm")]
    public void NativeDemuxerFormats_UseNativeDemuxerFlagTrue(string extension)
    {
        AudioFormatRegistry.UsesNativeDemuxer(extension).Should().BeTrue();
        AudioFormatRegistry.GetLibVlcHint(extension).Should().BeNull();
    }

    [Theory]
    [InlineData(".dsf", "dsf")]
    [InlineData(".dff", "dsf")]
    public void DsdFormats_MapToDsfAvFormatHint(string extension, string expectedHint)
    {
        AudioFormatRegistry.UsesNativeDemuxer(extension).Should().BeFalse();
        AudioFormatRegistry.GetLibVlcHint(extension).Should().Be(expectedHint);
    }

    [Theory]
    [InlineData(".mp3", "mp3")]
    [InlineData(".flac", "flac")]
    [InlineData(".wav", "wav")]
    [InlineData(".aac", "aac")]
    [InlineData(".m4a", "mp4")]
    [InlineData(".m4b", "mp4")]
    [InlineData(".mp4", "mp4")]
    [InlineData(".m4v", "mp4")]
    [InlineData(".wma", "asf")]
    [InlineData(".asf", "asf")]
    [InlineData(".aiff", "aiff")]
    [InlineData(".ape", "ape")]
    [InlineData(".wv", "wv")]
    [InlineData(".mpeg", "mpeg")]
    [InlineData(".mpg", "mpeg")]
    [InlineData(".mpe", "mpeg")]
    public void AvCodecDemuxerFormats_MapToAccurateHint(string extension, string expectedHint)
    {
        AudioFormatRegistry.UsesNativeDemuxer(extension).Should().BeFalse();
        AudioFormatRegistry.GetLibVlcHint(extension).Should().Be(expectedHint);
    }

    [Theory]
    [InlineData(".mpc")]
    [InlineData(".mpp")]
    [InlineData(".aa")]
    public void ProbedFormats_UseNeitherNativeNorFixedHint(string extension)
    {
        AudioFormatRegistry.UsesNativeDemuxer(extension).Should().BeFalse();
        AudioFormatRegistry.GetLibVlcHint(extension).Should().BeNull();
    }

    [Fact]
    public void AllMusicFileExtensions_HaveDeterministicPlaybackStrategy()
    {
        foreach (var extension in FileExtensions.MusicFileExtensions)
        {
            var isNative = AudioFormatRegistry.UsesNativeDemuxer(extension);
            var hint = AudioFormatRegistry.GetLibVlcHint(extension);

            if (isNative)
            {
                hint.Should().BeNull($"Format {extension} using native demuxer should not specify an avformat hint");
            }
            else
            {
                // Either has a known avformat hint OR relies on deliberate stream probing (mpc, mpp, aa)
                var isDeliberatelyProbed = extension is ".mpc" or ".mpp" or ".aa";
                if (!isDeliberatelyProbed)
                {
                    hint.Should().NotBeNullOrWhiteSpace($"Non-probed format {extension} must have a valid LibVLC avformat hint");
                }
            }
        }
    }
}
