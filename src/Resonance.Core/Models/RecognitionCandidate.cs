namespace Resonance.Core.Models;

using System;

/// <summary>
///     Representa uma gravação sugerida pelo serviço de reconhecimento acústico.
/// </summary>
public class RecognitionCandidate
{
    public string? AcoustId { get; init; }
    public string MusicBrainzTrackId { get; init; } = string.Empty;
    public string? MusicBrainzReleaseId { get; init; }
    public string? MusicBrainzArtistId { get; init; }

    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string? Album { get; init; }
    public int? Year { get; init; }

    /// <summary>
    ///     Pontuação de similaridade retornada pelo AcoustID (0.0 a 1.0).
    /// </summary>
    public double ConfidenceScore { get; init; }

    /// <summary>
    ///     Pontuação normalizada em percentual (0 a 100).
    /// </summary>
    public int ConfidencePercentage => (int)Math.Round(ConfidenceScore * 100);

    /// <summary>
    ///     Indica se o candidato atende ao critério de alta relevância (>= 80%).
    /// </summary>
    public bool IsHighConfidence => ConfidenceScore >= 0.80;
}
