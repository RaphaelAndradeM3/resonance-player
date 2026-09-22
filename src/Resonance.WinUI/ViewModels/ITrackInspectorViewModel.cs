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

    // Recognition / Fingerprint State
    bool IsRecognizing { get; }
    string RecognitionStatusText { get; }
    IReadOnlyList<RecognitionCandidate> RecognitionCandidates { get; }
    string? FingerprintHash { get; }
    bool IsAlreadyIdentified { get; }

    // Control Commands
    IAsyncRelayCommand ToggleInspectorCommand { get; }
    IAsyncRelayCommand CloseInspectorCommand { get; }
    IAsyncRelayCommand<Song> InspectSongCommand { get; }
    IAsyncRelayCommand<IReadOnlyList<Song>> InspectMultipleSongsCommand { get; }
    IAsyncRelayCommand NextSelectedTrackCommand { get; }
    IAsyncRelayCommand PreviousSelectedTrackCommand { get; }
    IAsyncRelayCommand ExportArtworkCommand { get; }
    IRelayCommand<string> CopyToClipboardCommand { get; }

    // Recognition Commands
    IAsyncRelayCommand IdentifyTrackCommand { get; }
    IAsyncRelayCommand ReidentifyTrackCommand { get; }
    IAsyncRelayCommand<RecognitionCandidate> SelectCandidateCommand { get; }
    IRelayCommand DiscardCandidatesCommand { get; }

    // Online Metadata Enrichment State (Feature 005)
    bool IsEnriching { get; }
    string EnrichmentStatusText { get; }
    EnrichmentProposal? CurrentProposal { get; }
    bool HasEnrichmentProposal { get; }
    string EnrichmentButtonText { get; }

    // Online Metadata Enrichment Commands (Feature 005)
    IAsyncRelayCommand FetchMetadataCommand { get; }
    IAsyncRelayCommand AdvanceToTagReviewCommand { get; }
    IRelayCommand ClearProposalCommand { get; }
}
