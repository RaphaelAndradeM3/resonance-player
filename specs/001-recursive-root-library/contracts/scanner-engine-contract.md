# Contract: Scanner Engine Interface

**Feature**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)  
**Contract ID**: `CTR-SCAN-001`  
**Namespace**: `Resonance.Core.Services.Abstractions`  

---

## 1. Visão Geral

Este contrato formaliza as interfaces e assinaturas de métodos responsáveis pela descoberta recursiva de arquivos, extração de metadados com concorrência limitada, controle de cancelamento cooperativo e emissão de telemetria de progresso para a UI.

---

## 2. Assinatura da Interface `ILibraryScanner`

A interface existente `ILibraryScanner` é mantida e complementada para assegurar os novos requisitos de resiliência a ciclos e cancelamento:

```csharp
namespace Resonance.Core.Services.Abstractions;

public interface ILibraryScanner
{
    /// <summary>
    /// Evento disparado quando o conteúdo da biblioteca é modificado (faixas adicionadas, atualizadas ou removidas).
    /// </summary>
    event EventHandler<LibraryContentChangedEventArgs>? LibraryContentChanged;

    /// <summary>
    /// Executa a varredura recursiva completa de uma pasta raiz específica.
    /// </summary>
    /// <param name="folderPath">Caminho físico da pasta raiz.</param>
    /// <param name="progress">Canal de reporte de telemetria de progresso em tempo real.</param>
    /// <param name="cancellationToken">Token de cancelamento cooperativo.</param>
    Task ScanFolderForMusicAsync(
        string folderPath, 
        IProgress<ScanProgress>? progress = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reexecuta a varredura incremental de uma pasta raiz cadastrada a partir do seu identificador.
    /// </summary>
    Task<bool> RescanFolderForMusicAsync(
        Guid folderId, 
        IProgress<ScanProgress>? progress = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa a varredura incremental de todas as pastas raiz ativas cadastradas.
    /// Raízes inacessíveis/desconectadas são puladas com aviso e seu catálogo é preservado.
    /// </summary>
    Task<bool> RefreshAllFoldersAsync(
        IProgress<ScanProgress>? progress = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Desduplica registros que apontem para o mesmo arquivo físico sob representações distintas de caminho.
    /// </summary>
    Task<int> DeduplicateLibraryAsync(CancellationToken cancellationToken = default);
}
```

---

## 3. Contrato do `SafeFileEnumerator` (Travessia Recursiva com Detecção de Ciclo)

O enumerador estático `SafeFileEnumerator` em `Resonance.Core.Helpers` deve expor o método de enumeração de arquivos protegida contra reparse points cíclicos:

```csharp
namespace Resonance.Core.Helpers;

public static class SafeFileEnumerator
{
    /// <summary>
    /// Enumera recursivamente arquivos que casem com a máscara de busca a partir de uma pasta raiz,
    /// resolvendo junções de diretório (NTFS Junctions) e links simbólicos através de seus destinos físicos
    /// reais e descartando nós já visitados para impedir loops infinitos.
    /// </summary>
    /// <param name="rootPath">Diretório raiz de início da varredura.</param>
    /// <param name="searchPattern">Padrão de busca (ex: "*.*").</param>
    /// <param name="searchOption">Opção de busca recursiva (AllDirectories).</param>
    /// <returns>Sequência preguiçosa de tuplas contendo caminho absoluto e LastWriteTimeUtc.</returns>
    public static IEnumerable<(string Path, DateTime LastWriteTimeUtc)> EnumerateFilesWithLastWriteTime(
        string rootPath,
        string searchPattern,
        SearchOption searchOption);

    /// <summary>
    /// Resolve o destino físico canônico de um diretório, seguindo links simbólicos ou junções se aplicável.
    /// Retorna null caso o caminho seja inválido ou inacessível.
    /// </summary>
    public static string? TryResolveCanonicalPath(DirectoryInfo directory);
}
```

---

## 4. Garantias e Pré-condições

1. **Prevenção de Ciclos**: O conjunto de caminhos canônicos visitados (`visitedSet`) deve ser isolado por operação de varredura de cada raiz.
2. **Tempo de Resposta ao Cancelamento**: A checagem de `cancellationToken.IsCancellationRequested` deve ocorrer a cada arquivo enumerado e antes do commit de cada lote de persistência, garantindo cancelamento efetivo em menos de 1000ms.
3. **Persistência Transacional em Lotes**: Lotes de inserção e atualização devem ser de no máximo 100 faixas por transação do SQLite para não travar leituras concorrentes do player.
4. **Isolamento de Erros**: Falhas de I/O em arquivos individuais (ex: `UnauthorizedAccessException`, arquivo corrompido) devem ser capturadas localmente e registradas no log sem abortar o enumerador.
