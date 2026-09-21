using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Resonance.Core.Models;

namespace Resonance.WinUI.ViewModels;

public interface ITrackInspectorViewModel
{
    // Display State
    bool IsOpen { get; set; }
    bool IsLoading { get; }
    string? ErrorMessage { get; }

    // Active Data
    TrackInspectorViewData? CurrentData { get; }
    bool FollowPlayback { get; set; }

    // Cover Art LightBox State
    bool IsLightBoxOpen { get; set; }

    // Control Commands
    IAsyncRelayCommand ToggleInspectorCommand { get; }
    IAsyncRelayCommand CloseInspectorCommand { get; }
    IAsyncRelayCommand<Song> InspectSongCommand { get; }
    IAsyncRelayCommand<IReadOnlyList<Song>> InspectMultipleSongsCommand { get; }
    IAsyncRelayCommand NextSelectedTrackCommand { get; }
    IAsyncRelayCommand PreviousSelectedTrackCommand { get; }
    IAsyncRelayCommand ExportArtworkCommand { get; }
    IRelayCommand<string> CopyToClipboardCommand { get; }
}
