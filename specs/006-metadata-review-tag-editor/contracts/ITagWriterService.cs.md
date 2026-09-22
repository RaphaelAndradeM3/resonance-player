# Contract: `ITagWriterService`

**Namespace**: `Resonance.Core.Services.Abstractions`  
**Assembly**: `Resonance.Core`  
**Spec Reference**: [spec.md](../spec.md) | [research.md](../research.md)

---

```csharp
using System.Threading;
using System.Threading.Tasks;
using Resonance.Core.Models;

namespace Resonance.Core.Services.Abstractions;

/// <summary>
///     Serviço de alta criticidade responsável pela gravação física, segura e atômica
///     de tags em arquivos de áudio, coordenação de locks de processo e sincronização com o banco.
/// </summary>
public interface ITagWriterService
{
    /// <summary>
    ///     Executa a gravação física segura com base no plano aprovado, utilizando arquivo temporário,
    ///     validação pós-escrita com ATL e substituição atômica no sistema de arquivos.
    /// </summary>
    /// <param name="plan">Plano de gravação validado com os campos aprovados pelo usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultado da operação com telemetria e indicação de sucesso/erro.</returns>
    Task<TagWriteResult> ApplyWritePlanAsync(
        TagWritePlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifica a integridade de um arquivo de áudio utilizando o leitor do ATL,
    ///     assegurando que o container é válido, headers não estão corrompidos e a duração é positiva.
    /// </summary>
    /// <param name="filePath">Caminho físico absoluto do arquivo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>True se o arquivo for perfeitamente legível e íntegro; false caso contrário.</returns>
    Task<bool> ValidateAudioFileIntegrityAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
```
