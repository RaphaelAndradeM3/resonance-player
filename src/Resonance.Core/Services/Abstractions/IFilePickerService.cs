namespace Resonance.Core.Services.Abstractions;

/// <summary>
///     Defines the contract for picking files from the storage filesystem in an abstracted manner.
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    ///     Prompts the user to pick a single file matching the given file extensions.
    /// </summary>
    /// <param name="fileTypes">Allowed file extensions (e.g., ".jpg", ".png").</param>
    /// <returns>The physical path of the picked file, or null if cancelled.</returns>
    Task<string?> PickSingleFileAsync(IEnumerable<string> fileTypes);
}
