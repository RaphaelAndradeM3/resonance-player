namespace Resonance.Core.Models.Lyrics;

/// <summary>
///     Resultado retornado por provedores online de letras (LRCLIB, NetEase),
///     indicando se a faixa possui letra sincronizada, texto puro ou foi identificada como instrumental.
/// </summary>
public class OnlineLyricsResult
{
    /// <summary>Conteúdo LRC com timestamps sincronizados, se disponível.</summary>
    public string? SyncedLyrics { get; set; }

    /// <summary>Texto puro da letra sem marcações de tempo, se disponível.</summary>
    public string? PlainLyrics { get; set; }

    /// <summary>Indica se a faixa foi confirmada como puramente instrumental pelo provedor.</summary>
    public bool IsInstrumental { get; set; }

    /// <summary>Verdadeiro se houver qualquer conteúdo de letra (sincronizada ou texto puro).</summary>
    public bool HasLyrics => !string.IsNullOrWhiteSpace(SyncedLyrics) || !string.IsNullOrWhiteSpace(PlainLyrics);
}
