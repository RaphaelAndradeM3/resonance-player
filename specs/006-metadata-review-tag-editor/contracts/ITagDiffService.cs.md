# Contract: `ITagDiffService`

**Namespace**: `Resonance.Core.Services.Abstractions`  
**Assembly**: `Resonance.Core`  
**Spec Reference**: [spec.md](../spec.md) | [data-model.md](../data-model.md)

---

```csharp
using System.Threading;
using System.Threading.Tasks;
using Resonance.Core.Models;

namespace Resonance.Core.Services.Abstractions;

/// <summary>
///     Serviço responsável por comparar os metadados atuais de um arquivo físico com valores propostos
///     (seja de enriquecimento online ou edição manual) e consolidar o plano de gravação de tags.
/// </summary>
public interface ITagDiffService
{
    /// <summary>
    ///     Gera uma coleção detalhada de diferenças campo a campo entre as tags atuais e uma proposta de enriquecimento online.
    /// </summary>
    /// <param name="filePath">Caminho físico absoluto do arquivo de áudio.</param>
    /// <param name="currentTags">Tags atuais lidas do arquivo ou persistidas no catálogo.</param>
    /// <param name="proposal">Proposta de enriquecimento originada do MusicBrainz (Feature 005).</param>
    /// <returns>Lista de registros diferenciais com flags de inclusão predefinidas com base nas alterações ativas.</returns>
    IReadOnlyList<TagDiffRecord> GenerateDiff(
        string filePath,
        TrackAudioTags currentTags,
        EnrichmentProposal proposal);

    /// <summary>
    ///     Gera uma coleção detalhada de diferenças campo a campo comparando as tags originais com os valores digitados no formulário.
    /// </summary>
    /// <param name="filePath">Caminho físico absoluto do arquivo de áudio.</param>
    /// <param name="originalTags">Tags originais lidas do arquivo.</param>
    /// <param name="editedModel">Valores editados pelo usuário no formulário do Tag Editor.</param>
    /// <returns>Lista de registros diferenciais com status e campos alterados marcados como selecionados.</returns>
    IReadOnlyList<TagDiffRecord> GenerateDiff(
        string filePath,
        TrackAudioTags originalTags,
        EditableTagModel editedModel);

    /// <summary>
    ///     Monta um <see cref="TagWritePlan"/> executável a partir dos registros diferenciais aprovados pelo usuário.
    /// </summary>
    /// <param name="filePath">Caminho físico absoluto do arquivo.</param>
    /// <param name="diffRecords">Coleção completa de campos do diff.</param>
    /// <param name="newPictureBytes">Bytes da imagem frontal aprovada para embutir, se houver.</param>
    /// <param name="pictureMimeType">Tipo MIME da imagem (ex: image/jpeg).</param>
    /// <param name="songId">ID da faixa na biblioteca local (SQLite), se indexada.</param>
    /// <returns>Plano de escrita validado e pronto para a execução atômica.</returns>
    TagWritePlan CreateWritePlan(
        string filePath,
        IEnumerable<TagDiffRecord> diffRecords,
        byte[]? newPictureBytes = null,
        string? pictureMimeType = null,
        Guid? songId = null);
}
```
