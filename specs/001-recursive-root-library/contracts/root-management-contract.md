# Contract: Root Management & Overlap Consolidation

**Feature**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)  
**Contract ID**: `CTR-ROOT-002`  
**Namespace**: `Resonance.Core.Helpers`, `Resonance.Core.Services.Abstractions`  

---

## 1. Visão Geral

Este contrato estabelece as regras para validação, adição e consolidação de pastas raiz da biblioteca, impedindo que pastas sobrepostas (ancestrais e descendentes) sejam registradas simultaneamente no catálogo.

---

## 2. Contrato de Validação de Sobreposição (`RootOverlapValidator`)

```csharp
namespace Resonance.Core.Helpers;

public enum RootOverlapAction
{
    /// <summary>
    /// A nova pasta é válida e não conflita com nenhuma raiz existente.
    /// </summary>
    Valid,

    /// <summary>
    /// A nova pasta é uma subpasta de uma raiz já cadastrada (deve ser rejeitada com aviso amigável).
    /// </summary>
    RejectSubfolderAlreadyCovered,

    /// <summary>
    /// A nova pasta é ancestral de uma ou mais raízes já cadastradas (as raízes filhas devem ser absorvidas/removidas).
    /// </summary>
    ConsolidateParent
}

public static class RootOverlapValidator
{
    /// <summary>
    /// Avalia a relação entre uma nova pasta candidata e as pastas raiz já cadastradas.
    /// </summary>
    /// <param name="candidatePath">Caminho canônico da nova pasta.</param>
    /// <param name="existingRootPaths">Coleção de caminhos canônicos das raízes atuais.</param>
    /// <returns>Resultado da avaliação com a ação recomendada e a lista de pastas conflitantes.</returns>
    public static RootOverlapResult Evaluate(
        string candidatePath, 
        IEnumerable<string> existingRootPaths);

    /// <summary>
    /// Retorna verdadeiro se o caminho 'potentialChild' for uma subpasta estrita de 'potentialParent'.
    /// </summary>
    public static bool IsSubdirectoryOf(string potentialChild, string potentialParent);
}

public record RootOverlapResult(
    RootOverlapAction Action, 
    string CandidatePath, 
    IReadOnlyList<string> ConflictingPaths);
```

---

## 3. Regras de Transição e Gestão na UI / ViewModel

1. **Tentativa de adicionar subpasta de raiz existente (`RejectSubfolderAlreadyCovered`)**:
   - A UI exibe uma notificação suave (*InfoBar* ou *Toast*): *"Esta pasta já está inclusa na biblioteca através de: {ConflictingPath}"*.
   - Nenhuma alteração é persistida na tabela `Folders`.
2. **Adição de pasta pai de raízes existentes (`ConsolidateParent`)**:
   - As raízes filhas listadas em `ConflictingPaths` são removidas do banco de dados (e as suas faixas têm seu `FolderId` reassociado à nova raiz ancestral unificada).
   - A nova pasta pai é cadastrada como a única raiz representativa daquela árvore.
3. **Remoção de Raiz**:
   - Ao remover uma pasta raiz, o sistema exibe opção para expurgar suas faixas ou mantê-las até o próximo ciclo.
