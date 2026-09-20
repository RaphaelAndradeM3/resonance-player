using System.Threading.Tasks;

namespace Resonance.WinUI.Services.Abstractions;

/// <summary>
///     Abstracts application-level lifecycle and navigation events.
/// </summary>
public interface IApplicationLifecycle
{
    Task NavigateToMainContentAsync();
    Task ResetAndRestartAsync();
}
