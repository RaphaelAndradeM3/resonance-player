using System;
using System.Collections.Generic;
using Resonance.Core.Constants;

namespace Resonance.Core.Helpers;

/// <summary>
///     Categorization of audio codecs based on compression and fidelity characteristics.
/// </summary>
public enum AudioCodecCategory
{
    Lossy,
    Lossless,
    HiResDsd
}

/// <summary>
///     Represents the capability, demuxing parameters, and display properties of an audio format.
/// </summary>
public readonly record struct AudioFormatCapability(
    string Extension,
    string DisplayName,
    AudioCodecCategory Category,
    string? LibVlcHint,
    bool UsesNativeDemuxer
);

/// <summary>
///     Provides O(1) format lookup, classification, and playback parameters for all audio
///     extensions supported by Resonance. Aligned directly with <see cref="FileExtensions.MusicFileExtensions"/>.
/// </summary>
public static class AudioFormatRegistry
{
    private static readonly Dictionary<string, AudioFormatCapability> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp3"] = new(".mp3", "MP3", AudioCodecCategory.Lossy, "mp3", false),
        [".flac"] = new(".flac", "FLAC", AudioCodecCategory.Lossless, "flac", false),
        [".wav"] = new(".wav", "WAV", AudioCodecCategory.Lossless, "wav", false),
        [".aac"] = new(".aac", "AAC", AudioCodecCategory.Lossy, "aac", false),
        [".m4a"] = new(".m4a", "M4A", AudioCodecCategory.Lossy, "mp4", false),
        [".m4b"] = new(".m4b", "M4B", AudioCodecCategory.Lossy, "mp4", false),
        [".mp4"] = new(".mp4", "MP4", AudioCodecCategory.Lossy, "mp4", false),
        [".m4v"] = new(".m4v", "M4V", AudioCodecCategory.Lossy, "mp4", false),
        [".ogg"] = new(".ogg", "Ogg", AudioCodecCategory.Lossy, null, true),
        [".oga"] = new(".oga", "Oga", AudioCodecCategory.Lossy, null, true),
        [".opus"] = new(".opus", "Opus", AudioCodecCategory.Lossy, null, true),
        [".webm"] = new(".webm", "WebM", AudioCodecCategory.Lossy, null, true),
        [".wma"] = new(".wma", "WMA", AudioCodecCategory.Lossy, "asf", false),
        [".asf"] = new(".asf", "ASF", AudioCodecCategory.Lossy, "asf", false),
        [".aiff"] = new(".aiff", "AIFF", AudioCodecCategory.Lossless, "aiff", false),
        [".ape"] = new(".ape", "Monkey's Audio", AudioCodecCategory.Lossless, "ape", false),
        [".wv"] = new(".wv", "WavPack", AudioCodecCategory.Lossless, "wv", false),
        [".dsf"] = new(".dsf", "DSD (DSF)", AudioCodecCategory.HiResDsd, "dsf", false),
        [".dff"] = new(".dff", "DSD (DFF)", AudioCodecCategory.HiResDsd, "dsf", false),
        [".mpc"] = new(".mpc", "Musepack", AudioCodecCategory.Lossy, null, false),
        [".mpp"] = new(".mpp", "Musepack", AudioCodecCategory.Lossy, null, false),
        [".aa"] = new(".aa", "Audible", AudioCodecCategory.Lossy, null, false),
        [".mpeg"] = new(".mpeg", "MPEG", AudioCodecCategory.Lossy, "mpeg", false),
        [".mpg"] = new(".mpg", "MPEG", AudioCodecCategory.Lossy, "mpeg", false),
        [".mpe"] = new(".mpe", "MPEG", AudioCodecCategory.Lossy, "mpeg", false)
    };

    /// <summary>
    ///     Checks if an extension is registered in the audio capability registry.
    /// </summary>
    public static bool IsSupported(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        var normalized = NormalizeExtension(extension);
        return Formats.ContainsKey(normalized);
    }

    /// <summary>
    ///     Tries to retrieve the capability descriptor for an audio extension.
    /// </summary>
    public static bool TryGetCapability(string? extension, out AudioFormatCapability capability)
    {
        capability = default;
        if (string.IsNullOrWhiteSpace(extension)) return false;
        var normalized = NormalizeExtension(extension);
        return Formats.TryGetValue(normalized, out capability);
    }

    /// <summary>
    ///     Returns the user-friendly display name for an audio format.
    /// </summary>
    public static string GetDisplayName(string? extension)
    {
        if (TryGetCapability(extension, out var cap)) return cap.DisplayName;
        return string.IsNullOrWhiteSpace(extension) ? "Unknown" : extension.TrimStart('.').ToUpperInvariant();
    }

    /// <summary>
    ///     Determines whether an audio extension belongs to a lossless or Hi-Res/DSD category.
    /// </summary>
    public static bool IsLossless(string? extension)
    {
        if (TryGetCapability(extension, out var cap))
        {
            return cap.Category is AudioCodecCategory.Lossless or AudioCodecCategory.HiResDsd;
        }
        return false;
    }

    /// <summary>
    ///     Returns the audio codec category (Lossy, Lossless, HiResDsd) for an extension.
    /// </summary>
    public static AudioCodecCategory? GetCategory(string? extension)
    {
        if (TryGetCapability(extension, out var cap)) return cap.Category;
        return null;
    }

    /// <summary>
    ///     Returns the AvFormat demuxer hint for LibVLC, or null if native/probing should be used.
    /// </summary>
    public static string? GetLibVlcHint(string? extension)
    {
        if (TryGetCapability(extension, out var cap)) return cap.LibVlcHint;
        return null;
    }

    /// <summary>
    ///     Determines whether LibVLC requires native demuxer for this extension.
    /// </summary>
    public static bool UsesNativeDemuxer(string? extension)
    {
        if (TryGetCapability(extension, out var cap)) return cap.UsesNativeDemuxer;
        return false;
    }

    private static string NormalizeExtension(string extension)
    {
        var trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed : $".{trimmed}";
    }
}
