# Interface Contract: `ILrcService`

## Namespace
`Resonance.Core.Services.Abstractions`

## Overview
Contrato central para carregamento, resolução canônica de 6 etapas, calibração de offset, exportação para sidecar e sincronização em tempo real de letras.

```csharp
using Resonance.Core.Models;
using Resonance.Core.Models.Lyrics;

namespace Resonance.Core.Services.Abstractions;

/// <summary>
///     Serviço unificado para resolução canônica, parsing, cache, calibração e exportação de letras.
/// </summary>
public interface ILrcService
{
    // ==========================================
    // Nova API Unificada (Feature 007)
    // ==========================================

    /// <summary>
    ///     Executa a resolução canônica de 6 etapas para a faixa especificada:
    ///     1. Embedded Synced -> 2. Embedded Plain -> 3. Sidecar .lrc -> 4. Sidecar .txt -> 5. Local Cache -> 6. Remote Providers.
    /// </summary>
    /// <param name="song">Objeto da música com metadados e caminhos.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Documento com linhas, proveniência e metadados, ou null se não encontrada.</returns>
    Task<LyricsDocument?> ResolveLyricsAsync(Song song, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Exporta explicitamente a letra em exibição atual para um arquivo sidecar (.lrc)
    ///     no mesmo diretório do arquivo de áudio da música.
    /// </summary>
    /// <param name="song">A faixa alvo.</param>
    /// <param name="lrcContent">Conteúdo formatado em LRC a ser gravado.</param>
    /// <returns>True se gravado com sucesso, false caso contrário.</returns>
    Task<bool> ExportSidecarLrcAsync(Song song, string lrcContent);

    /// <summary>
    ///     Atualiza e persiste o offset de calibração temporal da música no banco/cache local.
    /// </summary>
    /// <param name="song">A faixa alvo.</param>
    /// <param name="offsetMs">Offset em milissegundos (+ adianta, - atrasa).</param>
    Task SetLyricsOffsetAsync(Song song, int offsetMs);

    // ==========================================
    // Métodos Existentes Preservados (Compatibilidade)
    // ==========================================

    /// <summary>
    ///     Carrega e parseia letras para a música (retrocompatível com chamadas existentes).
    /// </summary>
    Task<ParsedLrc?> GetLyricsAsync(Song song, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Carrega e parseia diretamente um arquivo .lrc do caminho fornecido.
    /// </summary>
    Task<ParsedLrc?> GetLyricsAsync(string lrcFilePath);

    /// <summary>Salva letra customizada no cache do Resonance.</summary>
    Task SaveLyricsAsync(Song song, string lrcContent);

    /// <summary>Remove letra do cache sem apagar arquivos sidecar externos.</summary>
    Task<bool> RemoveCachedLyricsAsync(Song song);

    /// <summary>Indica se a música possui letra gerenciada no cache interno.</summary>
    bool HasCachedLyrics(Song song);

    /// <summary>Parseia string bruta LRC em objeto ParsedLrc.</summary>
    ParsedLrc ParseLyrics(string? lrcContent);

    /// <summary>Obtém a estrofe ativa com base no tempo atual de reprodução.</summary>
    LyricLine? GetCurrentLine(ParsedLrc parsedLrc, TimeSpan currentTime);

    /// <summary>Obtém a estrofe ativa com hint sequencial para otimização.</summary>
    LyricLine? GetCurrentLine(ParsedLrc parsedLrc, TimeSpan currentTime, ref int searchStartIndex);
}
```
