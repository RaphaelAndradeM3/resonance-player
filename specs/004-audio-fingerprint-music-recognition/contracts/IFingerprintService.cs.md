# Contract: `IFingerprintService`

**Namespace**: `Resonance.Core.Services.Abstractions`  
**File**: `src/Resonance.Core/Services/Abstractions/IFingerprintService.cs`  

```csharp
namespace Resonance.Core.Services.Abstractions;

using Resonance.Core.Models;

/// <summary>
///     Serviço responsável pelo cálculo 100% local de impressões digitais acústicas (Chromaprint).
/// </summary>
public interface IFingerprintService
{
    /// <summary>
    ///     Gera a impressão digital Chromaprint e obtém a duração da gravação a partir de um arquivo de áudio.
    ///     Nenhum dado sonoro deixa o computador local durante a execução.
    /// </summary>
    /// <param name="filePath">Caminho físico absoluto do arquivo de áudio.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    ///     A estrutura <see cref="AcousticFingerprint"/> contendo o hash Base64 e a duração em segundos,
    ///     ou <c>null</c> se a extração falhar ou o arquivo for inválido/inacessível.
    /// </returns>
    Task<AcousticFingerprint?> GenerateFingerprintAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
```
