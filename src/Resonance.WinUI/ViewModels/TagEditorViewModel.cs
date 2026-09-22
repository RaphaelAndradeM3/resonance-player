using System.Net.Http;
using Microsoft.Extensions.Logging;
using Resonance.Core.Services.Abstractions;

namespace Resonance.WinUI.ViewModels;

/// <summary>
///     WinUI specialization of <see cref="Resonance.Core.ViewModels.TagEditorViewModel"/>.
/// </summary>
public partial class TagEditorViewModel : Resonance.Core.ViewModels.TagEditorViewModel
{
    public TagEditorViewModel(
        ITagDiffService tagDiffService,
        ITagWriterService tagWriterService,
        IFilePickerService filePickerService,
        ILogger<TagEditorViewModel> logger,
        IHttpClientFactory? httpClientFactory = null)
        : base(tagDiffService, tagWriterService, filePickerService, logger, httpClientFactory)
    {
    }
}
