using Resonance.Core.Services.Data;

namespace Resonance.Core.Services.Abstractions;

public interface IBackupRestoreService
{
    Task<BackupResult> CreateBackupAsync(string destinationFolderPath);
    Task<RestoreResult> RestoreFromBackupAsync(string backupFilePath);
    Task<bool> ValidateBackupAsync(string backupFilePath);
}
