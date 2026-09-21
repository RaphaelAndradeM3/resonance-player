namespace Resonance.Core.Services.Implementations;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Resonance.Core.Http.AcoustId;
using Resonance.Core.Http.Pipelines;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;

/// <summary>
///     Cliente HTTP resiliente para consulta ao serviço AcoustID e mapeamento de candidatos do MusicBrainz.
/// </summary>
public class AcoustIdService : IAcoustIdService
{
    private const string AcoustIdApiUrl = "https://api.acoustid.org/v2/lookup";
    private const string DefaultClientKey = "8XaBELgH";
    private const string UserAgent = "Resonance-Player/1.0 (+https://github.com/RaphaelAndradeM3/resonance-player)";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IProviderPipelineProvider _pipelines;
    private readonly IApiKeyService _apiKeyService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<AcoustIdService> _logger;

    private readonly ConcurrentDictionary<(string Hash, int Duration), RecognitionResult> _cache = new();

    public AcoustIdService(
        IHttpClientFactory httpClientFactory,
        IProviderPipelineProvider pipelines,
        IApiKeyService apiKeyService,
        ISettingsService settingsService,
        ILogger<AcoustIdService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _pipelines = pipelines ?? throw new ArgumentNullException(nameof(pipelines));
        _apiKeyService = apiKeyService ?? throw new ArgumentNullException(nameof(apiKeyService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync()
    {
        var onlineMetadataEnabled = await _settingsService.GetFetchOnlineMetadataEnabledAsync().ConfigureAwait(false);
        if (!onlineMetadataEnabled)
        {
            return false;
        }

        var enabledProviders = await _settingsService.GetEnabledServiceProvidersAsync(ServiceCategory.Metadata).ConfigureAwait(false);
        return enabledProviders.Any(p => string.Equals(p.Id, ServiceProviderIds.AcoustId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<RecognitionResult> LookupAsync(
        AcousticFingerprint fingerprint,
        CancellationToken cancellationToken = default)
    {
        if (!fingerprint.IsValid)
        {
            return new RecognitionResult
            {
                Status = RecognitionStatus.AnalysisFailed,
                Fingerprint = fingerprint,
                ErrorMessage = "Impressão digital acústica inválida."
            };
        }

        if (fingerprint.DurationSeconds < 10)
        {
            return new RecognitionResult
            {
                Status = RecognitionStatus.AudioTooShort,
                Fingerprint = fingerprint,
                ErrorMessage = "A gravação de áudio é muito curta para identificação confiável (mínimo de 10 segundos)."
            };
        }

        if (!await IsEnabledAsync().ConfigureAwait(false))
        {
            _logger.LogInformation("Consulta AcoustID ignorada: provedor desativado ou offline.");
            return new RecognitionResult
            {
                Status = RecognitionStatus.OfflineOrDisabled,
                Fingerprint = fingerprint,
                ErrorMessage = "O serviço AcoustID está desativado nas configurações do player."
            };
        }

        var cacheKey = (fingerprint.Hash, fingerprint.DurationSeconds);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            _logger.LogDebug("Resultado do AcoustID recuperado do cache em memória para {Hash}", fingerprint.Hash);
            return cached;
        }

        string clientKey = await ResolveClientKeyAsync(cancellationToken).ConfigureAwait(false);

        var fallbackResult = new RecognitionResult
        {
            Status = RecognitionStatus.NetworkError,
            Fingerprint = fingerprint,
            ErrorMessage = "Falha de conexão com a API do AcoustID."
        };

        var result = await _pipelines.ExecuteWithFallbackAsync(
            ServiceProviderIds.AcoustId,
            async ct =>
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, AcoustIdApiUrl)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["client"] = clientKey,
                        ["meta"] = "recordings releasegroups",
                        ["duration"] = fingerprint.DurationSeconds.ToString(CultureInfo.InvariantCulture),
                        ["fingerprint"] = fingerprint.Hash
                    })
                };
                request.Headers.UserAgent.ParseAdd(UserAgent);
                return await client.SendAsync(request, ct).ConfigureAwait(false);
            },
            async (response, ct) =>
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("API do AcoustID retornou HTTP 429 (Too Many Requests).");
                    return new RecognitionResult
                    {
                        Status = RecognitionStatus.RateLimited,
                        Fingerprint = fingerprint,
                        ErrorMessage = "Limite de requisições do AcoustID atingido. Aguarde alguns instantes."
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AcoustID lookup falhou com status {StatusCode}.", response.StatusCode);
                    return new RecognitionResult
                    {
                        Status = RecognitionStatus.NetworkError,
                        Fingerprint = fingerprint,
                        ErrorMessage = $"AcoustID respondeu com status {(int)response.StatusCode}."
                    };
                }

                var apiResponse = await response.Content.ReadFromJsonAsync<AcoustIdLookupResponse>(cancellationToken: ct)
                    .ConfigureAwait(false);

                if (apiResponse == null || !string.Equals(apiResponse.Status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    var msg = apiResponse?.Error?.Message ?? "Resposta inválida do serviço AcoustID.";
                    _logger.LogWarning("AcoustID retornou erro da aplicação: {Message}", msg);
                    return new RecognitionResult
                    {
                        Status = RecognitionStatus.AnalysisFailed,
                        Fingerprint = fingerprint,
                        ErrorMessage = msg
                    };
                }

                var candidates = ExtractAndRankCandidates(apiResponse);

                if (candidates.Count == 0)
                {
                    return new RecognitionResult
                    {
                        Status = RecognitionStatus.NoMatchFound,
                        Fingerprint = fingerprint,
                        Candidates = Array.Empty<RecognitionCandidate>(),
                        ErrorMessage = "Nenhuma correspondência encontrada para esta gravação."
                    };
                }

                return new RecognitionResult
                {
                    Status = RecognitionStatus.Success,
                    Fingerprint = fingerprint,
                    Candidates = candidates
                };
            },
            fallbackResult,
            _logger,
            "AcoustID Lookup",
            cancellationToken).ConfigureAwait(false);

        if (result.Status is RecognitionStatus.Success or RecognitionStatus.NoMatchFound)
        {
            _cache[cacheKey] = result;
        }

        return result;
    }

    private async Task<string> ResolveClientKeyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var userKey = await _settingsService.GetAcoustIdUserApiKeyAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(userKey))
            {
                return userKey.Trim();
            }

            var appKey = await _apiKeyService.GetApiKeyAsync(ServiceProviderIds.AcoustId, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(appKey))
            {
                return appKey.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao resolver API Key do AcoustID. Utilizando chave padrão.");
        }

        return DefaultClientKey;
    }

    private static IReadOnlyList<RecognitionCandidate> ExtractAndRankCandidates(AcoustIdLookupResponse response)
    {
        if (response.Results == null || response.Results.Count == 0)
        {
            return Array.Empty<RecognitionCandidate>();
        }

        var candidateList = new List<RecognitionCandidate>();
        var seenTrackIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resultItem in response.Results)
        {
            // Filtro de corte mínimo: 40% de confiança
            if (resultItem.Score < 0.40)
            {
                continue;
            }

            if (resultItem.Recordings == null || resultItem.Recordings.Count == 0)
            {
                continue;
            }

            foreach (var recording in resultItem.Recordings)
            {
                if (string.IsNullOrWhiteSpace(recording.Id) || seenTrackIds.Contains(recording.Id))
                {
                    continue;
                }

                seenTrackIds.Add(recording.Id);

                var artistName = recording.Artists != null && recording.Artists.Count > 0
                    ? string.Join(", ", recording.Artists.Select(a => a.Name).Where(n => !string.IsNullOrWhiteSpace(n)))
                    : string.Empty;

                var releaseGroup = recording.ReleaseGroups?.FirstOrDefault();

                candidateList.Add(new RecognitionCandidate
                {
                    AcoustId = resultItem.Id,
                    MusicBrainzTrackId = recording.Id,
                    MusicBrainzReleaseId = releaseGroup?.Id,
                    MusicBrainzArtistId = recording.Artists?.FirstOrDefault()?.Id,
                    Title = recording.Title ?? "Gravação Desconhecida",
                    Artist = string.IsNullOrWhiteSpace(artistName) ? "Artista Desconhecido" : artistName,
                    Album = releaseGroup?.Title,
                    ConfidenceScore = resultItem.Score
                });
            }
        }

        return candidateList
            .OrderByDescending(c => c.ConfidenceScore)
            .Take(5)
            .ToList();
    }
}
