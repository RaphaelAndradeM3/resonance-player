using Resonance.Core.Models.Lyrics;

namespace Resonance.Core.Services.Abstractions;

public interface ILyricRomanizationService
{
    Task<IReadOnlyList<LyricLine>> ApplyRomanizationAsync(IEnumerable<LyricLine> lines, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LyricLine>> ApplyRomanizationAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default);
}
