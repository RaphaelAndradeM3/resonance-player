using System.Linq;

namespace Resonance.Core.Models.Lyrics;

/// <summary>
///     Representa o documento unificado de letras resolvido pelo motor para uma música em reprodução.
/// </summary>
public class LyricsDocument
{
    public LyricsDocument(
        IEnumerable<LyricLine> lines,
        string? rawUnsyncedLyrics = null,
        LyricsType type = LyricsType.None,
        LyricsProvenance provenance = LyricsProvenance.None,
        string? sourcePath = null,
        TimeSpan timeOffset = default,
        bool isInstrumental = false)
    {
        Lines = (lines ?? Enumerable.Empty<LyricLine>()).OrderBy(l => l.StartTime).ToList().AsReadOnly();
        RawUnsyncedLyrics = rawUnsyncedLyrics;
        Type = type;
        Provenance = provenance;
        SourcePath = sourcePath;
        TimeOffset = timeOffset;
        IsInstrumental = isInstrumental;
    }

    /// <summary>Lista imutável e ordenada de estrofes/versos com timestamps.</summary>
    public IReadOnlyList<LyricLine> Lines { get; }

    /// <summary>Texto completo para letras não-sincronizadas ou brutas.</summary>
    public string? RawUnsyncedLyrics { get; }

    /// <summary>Classificação da letra (Sincronizada, Texto Puro, Instrumental, Nenhuma).</summary>
    public LyricsType Type { get; }

    /// <summary>Origem exata da resolução canônica.</summary>
    public LyricsProvenance Provenance { get; }

    /// <summary>Caminho em disco do arquivo fonte ou cache (se aplicável).</summary>
    public string? SourcePath { get; }

    /// <summary>Offset de calibração temporal aplicado às linhas.</summary>
    public TimeSpan TimeOffset { get; }

    /// <summary>Indica se a faixa foi classificada como instrumental.</summary>
    public bool IsInstrumental { get; }

    /// <summary>Verdadeiro se não houver linhas sincronizadas nem texto puro.</summary>
    public bool IsEmpty => !Lines.Any() && string.IsNullOrWhiteSpace(RawUnsyncedLyrics);

    /// <summary>Instância estática para estado vazio/sem letra.</summary>
    public static LyricsDocument Empty { get; } = new(
        Enumerable.Empty<LyricLine>(),
        null,
        LyricsType.None,
        LyricsProvenance.None);

    /// <summary>Instância estática para faixa instrumental identificada.</summary>
    public static LyricsDocument CreateInstrumental(LyricsProvenance provenance = LyricsProvenance.LocalCache) => new(
        Enumerable.Empty<LyricLine>(),
        null,
        LyricsType.Instrumental,
        provenance,
        null,
        TimeSpan.Zero,
        true);
}
