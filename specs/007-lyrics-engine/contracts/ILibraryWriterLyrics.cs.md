# Interface Contract: `ILibraryWriter` Extensions

## Namespace
`Resonance.Core.Services.Abstractions`

## Overview
Extensões no contrato `ILibraryWriter` para suportar a persistência imediata de propriedades de letras (`LyricsOffsetMs` e `IsInstrumental`).

```csharp
namespace Resonance.Core.Services.Abstractions;

public partial interface ILibraryWriter
{
    /// <summary>
    ///     Atualiza o offset de calibração temporal de letras (em milissegundos) para a faixa especificada.
    /// </summary>
    /// <param name="songId">Identificador único da música.</param>
    /// <param name="offsetMs">Valor de compensação em ms (ou null para remover).</param>
    Task UpdateSongLyricsOffsetAsync(Guid songId, int? offsetMs);

    /// <summary>
    ///     Atualiza a sinalização de música instrumental para a faixa especificada.
    /// </summary>
    /// <param name="songId">Identificador único da música.</param>
    /// <param name="isInstrumental">Indica se a música é instrumental.</param>
    Task UpdateSongInstrumentalAsync(Guid songId, bool isInstrumental);
}
```
