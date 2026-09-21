namespace Resonance.Core.Models;

/// <summary>
///     Encapsula a impressão digital acústica calculada localmente pelo Chromaprint.
/// </summary>
/// <param name="Hash">String compactada em Base64 gerada pelo algoritmo.</param>
/// <param name="DurationSeconds">Duração da gravação em segundos inteiros.</param>
/// <param name="Algorithm">Versão do algoritmo Chromaprint (padrão = 1).</param>
public readonly record struct AcousticFingerprint(
    string Hash,
    int DurationSeconds,
    int Algorithm = 1)
{
    /// <summary>
    ///     Indica se o fingerprint é válido (hash preenchido e duração positiva).
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Hash) && DurationSeconds > 0;

    /// <summary>
    ///     Representa uma impressão digital vazia ou não calculada.
    /// </summary>
    public static AcousticFingerprint Empty => new(string.Empty, 0);
}
