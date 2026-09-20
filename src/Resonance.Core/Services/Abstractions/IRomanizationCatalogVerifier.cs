using Resonance.Core.Models.Romanization;

namespace Resonance.Core.Services.Abstractions;

public interface IRomanizationCatalogVerifier
{
    bool Verify(RomanizationCatalogEnvelope envelope);
}
