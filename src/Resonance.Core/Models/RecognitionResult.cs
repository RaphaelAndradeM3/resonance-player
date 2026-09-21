namespace Resonance.Core.Models;

using System;
using System.Collections.Generic;

public enum RecognitionStatus
{
    Success,
    NoMatchFound,
    OfflineOrDisabled,
    RateLimited,
    NetworkError,
    AudioTooShort,
    AnalysisFailed
}

/// <summary>
///     Resultado consolidado de uma operação de identificação acústica.
/// </summary>
public class RecognitionResult
{
    public RecognitionStatus Status { get; init; }
    public IReadOnlyList<RecognitionCandidate> Candidates { get; init; } = Array.Empty<RecognitionCandidate>();
    public AcousticFingerprint? Fingerprint { get; init; }
    public string? ErrorMessage { get; init; }

    public bool HasCandidates => Candidates.Count > 0;
}
