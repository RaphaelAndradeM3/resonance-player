# Contract: `IAcoustIdService`

**Namespace**: `Resonance.Core.Services.Abstractions`  
**File**: `src/Resonance.Core/Services/Abstractions/IAcoustIdService.cs`  

```csharp
namespace Resonance.Core.Services.Abstractions;

using Resonance.Core.Models;

/// <summary>
///     Cliente HTTP resiliente para consulta ao serviço AcoustID e mapeamento para candidatos do MusicBrainz.
/// </summary>
public interface IAcoustIdService
{
    /// <summary>
    ///     Consulta o catálogo público do AcoustID utilizando o fingerprint acústico e a duração da gravação.
    ///     Apenas a string do fingerprint e o tempo em segundos são transmitidos.
    /// </summary>
    /// <param name="fingerprint">A impressão digital previamente calculada.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    ///     O <see cref="RecognitionResult"/> contendo o status da consulta e os candidatos ordenados
    ///     com pontuações de similaridade e identificadores do MusicBrainz.
    /// </returns>
    Task<RecognitionResult> LookupAsync(
        AcousticFingerprint fingerprint,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifica se o serviço AcoustID está atualmente habilitado e configurado para consultas online.
    /// </summary>
    Task<bool> IsEnabledAsync();
}
```
