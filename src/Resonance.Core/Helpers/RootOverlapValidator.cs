namespace Resonance.Core.Helpers;

public enum RootOverlapAction
{
    /// <summary>
    ///     A nova pasta é válida e não conflita com nenhuma raiz existente.
    /// </summary>
    Valid,

    /// <summary>
    ///     A nova pasta é uma subpasta (ou idêntica) a uma raiz já cadastrada (deve ser rejeitada).
    /// </summary>
    RejectSubfolderAlreadyCovered,

    /// <summary>
    ///     A nova pasta é ancestral de uma ou mais raízes já cadastradas (as raízes filhas devem ser consolidadas).
    /// </summary>
    ConsolidateParent
}

public record RootOverlapResult(
    RootOverlapAction Action,
    string CandidatePath,
    IReadOnlyList<string> ConflictingPaths);

public static class RootOverlapValidator
{
    /// <summary>
    ///     Avalia a relação entre uma nova pasta candidata e as pastas raiz já cadastradas.
    /// </summary>
    public static RootOverlapResult Evaluate(string candidatePath, IEnumerable<string> existingRootPaths)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return new RootOverlapResult(RootOverlapAction.Valid, string.Empty, Array.Empty<string>());
        }

        var normCandidate = PathCanonicalizer.Normalize(candidatePath);
        var parents = new List<string>();
        var children = new List<string>();

        foreach (var existing in existingRootPaths)
        {
            if (string.IsNullOrWhiteSpace(existing)) continue;
            var normExisting = PathCanonicalizer.Normalize(existing);

            if (string.Equals(normCandidate, normExisting, StringComparison.OrdinalIgnoreCase))
            {
                parents.Add(normExisting);
            }
            else if (IsSubdirectoryOf(normCandidate, normExisting))
            {
                parents.Add(normExisting);
            }
            else if (IsSubdirectoryOf(normExisting, normCandidate))
            {
                children.Add(normExisting);
            }
        }

        if (parents.Count > 0)
        {
            return new RootOverlapResult(RootOverlapAction.RejectSubfolderAlreadyCovered, normCandidate, parents);
        }

        if (children.Count > 0)
        {
            return new RootOverlapResult(RootOverlapAction.ConsolidateParent, normCandidate, children);
        }

        return new RootOverlapResult(RootOverlapAction.Valid, normCandidate, Array.Empty<string>());
    }

    /// <summary>
    ///     Verifica se potentialChild é um subdiretório estrito de potentialParent.
    /// </summary>
    public static bool IsSubdirectoryOf(string potentialChild, string potentialParent)
    {
        if (string.IsNullOrWhiteSpace(potentialChild) || string.IsNullOrWhiteSpace(potentialParent))
        {
            return false;
        }

        var child = PathCanonicalizer.Normalize(potentialChild);
        var parent = PathCanonicalizer.Normalize(potentialParent);

        if (string.Equals(child, parent, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Garante que o diretório pai termine com separador de caminho para evitar falsos positivos
        // como "C:\MusicExtra" sendo considerado filho de "C:\Music"
        var parentWithSeparator = parent.EndsWith('\\') ? parent : parent + '\\';

        return child.StartsWith(parentWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
