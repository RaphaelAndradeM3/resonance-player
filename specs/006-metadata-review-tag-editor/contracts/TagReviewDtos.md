# Contract: Tag Review & Editor DTOs

**Namespace**: `Resonance.Core.Models`  
**Assembly**: `Resonance.Core`  
**Spec Reference**: [spec.md](../spec.md) | [data-model.md](../data-model.md)

---

## 1. `TagDiffRecord.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Resonance.Core.Models;

/// <summary>
///     Representa o diferencial de um campo individual entre o arquivo original e o valor proposto.
/// </summary>
public partial class TagDiffRecord : ObservableObject
{
    public string FieldKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? OriginalValue { get; init; }
    public string? ProposedValue { get; init; }
    public FieldProposalStatus Status { get; init; }
    public MetadataProvenance Provenance { get; init; }
    public bool IsPictureField { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public bool HasChanged => Status != FieldProposalStatus.Unchanged;
}
```

---

## 2. `TagWritePlan.cs`

```csharp
namespace Resonance.Core.Models;

/// <summary>
///     Conjunto imutável de instruções para aplicação física no arquivo de áudio.
/// </summary>
public class TagWritePlan
{
    public required string FilePath { get; init; }
    public Guid? SongId { get; init; }
    public required IReadOnlyList<TagDiffRecord> SelectedChanges { get; init; }
    public byte[]? NewPictureBytes { get; init; }
    public string? PictureMimeType { get; init; }
    public bool RemovePicture { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool HasChanges => SelectedChanges.Count > 0 || NewPictureBytes != null || RemovePicture;
}
```

---

## 3. `TagWriteResult.cs`

```csharp
namespace Resonance.Core.Models;

/// <summary>
///     Resultado da rotina de gravação física segura.
/// </summary>
public class TagWriteResult
{
    public bool Success { get; init; }
    public required string FilePath { get; init; }
    public int FieldsUpdatedCount { get; init; }
    public bool WasPlaybackInterrupted { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan ElapsedTime { get; init; }

    public static TagWriteResult Failed(string filePath, string errorMessage, TimeSpan elapsed) => new()
    {
        Success = false,
        FilePath = filePath,
        ErrorMessage = errorMessage,
        ElapsedTime = elapsed
    };

    public static TagWriteResult Succeeded(string filePath, int fieldsCount, bool wasPlaybackInterrupted, TimeSpan elapsed) => new()
    {
        Success = true,
        FilePath = filePath,
        FieldsUpdatedCount = fieldsCount,
        WasPlaybackInterrupted = wasPlaybackInterrupted,
        ElapsedTime = elapsed
    };
}
```

---

## 4. `EditableTagModel.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Resonance.Core.Models;

/// <summary>
///     Modelo mutável para o formulário manual do TagEditorDialog em WinUI.
/// </summary>
public partial class EditableTagModel : ObservableObject
{
    [ObservableProperty] public partial string? Title { get; set; }
    [ObservableProperty] public partial string? Artist { get; set; }
    [ObservableProperty] public partial string? Album { get; set; }
    [ObservableProperty] public partial string? AlbumArtist { get; set; }
    [ObservableProperty] public partial uint? Year { get; set; }
    [ObservableProperty] public partial uint? TrackNumber { get; set; }
    [ObservableProperty] public partial uint? TrackTotal { get; set; }
    [ObservableProperty] public partial uint? DiscNumber { get; set; }
    [ObservableProperty] public partial uint? DiscTotal { get; set; }
    [ObservableProperty] public partial string? Genre { get; set; }
    [ObservableProperty] public partial string? Comment { get; set; }
    [ObservableProperty] public partial byte[]? PictureBytes { get; set; }
    [ObservableProperty] public partial string? PictureMimeType { get; set; }
}
```
