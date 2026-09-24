namespace Resonance.Core.Models.Lyrics;

/// <summary>
///     Identifica a procedência da letra para exibição de selo na interface e auditoria de cache.
/// </summary>
public enum LyricsProvenance
{
    None = 0,
    EmbeddedSynced = 1,
    EmbeddedPlain = 2,
    LocalFileLrc = 3,
    LocalFileTxt = 4,
    LocalCache = 5,
    RemoteLrcLib = 6,
    RemoteNetEase = 7
}
