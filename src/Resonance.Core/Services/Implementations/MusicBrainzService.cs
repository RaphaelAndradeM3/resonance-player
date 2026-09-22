using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Resonance.Core.Helpers;
using Resonance.Core.Http.MusicBrainz;
using Resonance.Core.Http.Pipelines;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;

namespace Resonance.Core.Services.Implementations;

/// <summary>
///     Service for resolving artist identities, recording details, and release artwork via the MusicBrainz
///     and Cover Art Archive databases.
///     Rate limiting (1 req/s as MusicBrainz requires), retry, and circuit-breaker policy
///     are handled by <see cref="IProviderPipelineProvider"/>.
/// </summary>
public class MusicBrainzService : IMusicBrainzService
{
    private const string BaseUrl = "https://musicbrainz.org/ws/2";
    private const string CaaBaseUrl = "https://coverartarchive.org/release";
    private const string UserAgent = "Resonance/1.0 (+https://github.com/RaphaelAndradeM3/resonance-player)";

    private readonly HttpClient _httpClient;
    private readonly IProviderPipelineProvider _pipelines;
    private readonly ILogger<MusicBrainzService> _logger;
    private readonly string _cacheDirectory;

    public MusicBrainzService(
        IHttpClientFactory httpClientFactory,
        IProviderPipelineProvider pipelines,
        ILogger<MusicBrainzService> logger,
        IPathConfiguration? pathConfig = null)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _pipelines = pipelines;
        _logger = logger;

        _cacheDirectory = pathConfig?.MetadataCachePath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resonance", "MetadataCache");

        try
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create metadata cache directory at {Path}", _cacheDirectory);
        }
    }

    /// <inheritdoc />
    public async Task<string?> SearchArtistAsync(string artistName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return null;

        if (_pipelines.IsCircuitOpen(ServiceProviderIds.MusicBrainz))
        {
            _logger.LogDebug("MusicBrainz circuit is open; skipping lookup for {ArtistName}.", artistName);
            return null;
        }

        // Quote the artist name for multi-word names (Lucene syntax)
        var encodedName = Uri.EscapeDataString($"\"{artistName}\"");
        var url = $"{BaseUrl}/artist?query=artist:{encodedName}&limit=1&fmt=json";

        return await _pipelines.ExecuteWithFallbackAsync<string?>(
            ServiceProviderIds.MusicBrainz,
            async ct =>
            {
                _logger.LogDebug("Searching MusicBrainz for artist: {ArtistName}", artistName);
                return await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            },
            async (response, ct) =>
            {
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("MusicBrainz search failed with status {StatusCode} for artist: {ArtistName}",
                        response.StatusCode, artistName);
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<MusicBrainzArtistSearchResult>(cancellationToken: ct)
                    .ConfigureAwait(false);

                var artist = result?.Artists?.FirstOrDefault();
                if (artist is null)
                {
                    _logger.LogDebug("No MusicBrainz match found for artist: {ArtistName}", artistName);
                    return null;
                }

                if (artist.Score < 80)
                {
                    _logger.LogDebug("MusicBrainz match score too low ({Score}) for artist: {ArtistName}",
                        artist.Score, artistName);
                    return null;
                }

                _logger.LogInformation("Found MusicBrainz ID {MBID} for artist: {ArtistName}", artist.Id, artistName);
                return artist.Id;
            },
            fallback: null,
            _logger,
            $"search for {artistName}",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MusicBrainzRecordingDetail?> GetRecordingMetadataAsync(
        string recordingMbid,
        string? preferredAlbum = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recordingMbid))
            return null;

        var cacheKey = $"recording_{recordingMbid}_{(preferredAlbum != null ? ComputeHash(preferredAlbum) : "any")}";
        var cached = TryGetFromCache<MusicBrainzRecordingDetail>(cacheKey);
        if (cached is not null)
        {
            _logger.LogDebug("Metadata cache hit for recording {MBID}", recordingMbid);
            return cached;
        }

        if (_pipelines.IsCircuitOpen(ServiceProviderIds.MusicBrainz))
        {
            _logger.LogDebug("MusicBrainz circuit is open; skipping recording lookup for {MBID}.", recordingMbid);
            return null;
        }

        var url = $"{BaseUrl}/recording/{recordingMbid}?inc=releases+artists+media+isrcs+tags&fmt=json";

        return await _pipelines.ExecuteWithFallbackAsync<MusicBrainzRecordingDetail?>(
            ServiceProviderIds.MusicBrainz,
            async ct =>
            {
                _logger.LogDebug("Querying MusicBrainz recording details for MBID: {MBID}", recordingMbid);
                return await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            },
            async (response, ct) =>
            {
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("MusicBrainz recording lookup failed with status {StatusCode} for MBID: {MBID}",
                        response.StatusCode, recordingMbid);
                    return null;
                }

                var recording = await response.Content.ReadFromJsonAsync<MusicBrainzRecordingLookupResponse>(cancellationToken: ct)
                    .ConfigureAwait(false);

                if (recording is null || string.IsNullOrWhiteSpace(recording.Id))
                    return null;

                var detail = MapRecordingToDetail(recording, preferredAlbum);
                if (detail is not null)
                {
                    SaveToCache(cacheKey, detail);
                }

                return detail;
            },
            fallback: null,
            _logger,
            $"lookup recording {recordingMbid}",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MusicBrainzRecordingDetail?> SearchRecordingAsync(
        string artist,
        string title,
        string? preferredAlbum = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
            return null;

        var cacheKey = $"search_{ComputeHash($"{artist}_{title}_{preferredAlbum ?? ""}")}";
        var cached = TryGetFromCache<MusicBrainzRecordingDetail>(cacheKey);
        if (cached is not null)
        {
            _logger.LogDebug("Metadata cache hit for search {Artist} - {Title}", artist, title);
            return cached;
        }

        if (_pipelines.IsCircuitOpen(ServiceProviderIds.MusicBrainz))
        {
            _logger.LogDebug("MusicBrainz circuit is open; skipping search for {Artist} - {Title}.", artist, title);
            return null;
        }

        // Strip leading track numbers (e.g. "001 - ") and platform suffixes (e.g. " - YouTube")
        var effectiveTitle = System.Text.RegularExpressions.Regex.Replace(title.Trim(), @"^\d+[\s.-]+", "").Trim();
        effectiveTitle = System.Text.RegularExpressions.Regex.Replace(effectiveTitle, @"\s*-\s*YouTube$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        var effectiveArtist = artist.Trim();

        // If artist is unknown or placeholder, check if title has "Artist - Title" format
        if (IsUnknownArtist(effectiveArtist) && effectiveTitle.Contains(" - "))
        {
            var parts = effectiveTitle.Split(new[] { " - " }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            {
                effectiveArtist = parts[0].Trim();
                effectiveTitle = parts[1].Trim();
            }
        }

        // Escape Lucene special characters
        var cleanTitle = EscapeLucene(effectiveTitle);
        string query;
        if (IsUnknownArtist(effectiveArtist))
        {
            query = $"recording:\"{cleanTitle}\"";
        }
        else
        {
            var cleanArtist = EscapeLucene(effectiveArtist);
            query = $"recording:\"{cleanTitle}\" AND artist:\"{cleanArtist}\"";
        }
        var url = $"{BaseUrl}/recording?query={Uri.EscapeDataString(query)}&limit=5&fmt=json";

        return await _pipelines.ExecuteWithFallbackAsync<MusicBrainzRecordingDetail?>(
            ServiceProviderIds.MusicBrainz,
            async ct =>
            {
                _logger.LogDebug("Searching MusicBrainz recording for {Artist} - {Title}", artist, title);
                return await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            },
            async (response, ct) =>
            {
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("MusicBrainz recording search failed with status {StatusCode} for {Artist} - {Title}",
                        response.StatusCode, artist, title);
                    return null;
                }

                var searchResult = await response.Content.ReadFromJsonAsync<MusicBrainzRecordingSearchResponse>(cancellationToken: ct)
                    .ConfigureAwait(false);

                var bestMatch = searchResult?.Recordings?
                    .OrderByDescending(r => r.Score)
                    .FirstOrDefault();

                if (bestMatch is null || bestMatch.Score < 60)
                {
                    _logger.LogDebug("No confident MusicBrainz recording match found for {Artist} - {Title}", artist, title);
                    return null;
                }

                // Fetch full details using the matched Recording MBID to resolve complete release and media information
                var fullDetail = await GetRecordingMetadataAsync(bestMatch.Id, preferredAlbum, ct).ConfigureAwait(false);
                if (fullDetail is not null)
                {
                    SaveToCache(cacheKey, fullDetail);
                    return fullDetail;
                }

                return null;
            },
            fallback: null,
            _logger,
            $"search recording {artist} - {title}",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> GetCoverArtUrlAsync(string releaseMbid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(releaseMbid))
            return null;

        var cacheKey = $"caa_{releaseMbid}";
        var cached = TryGetFromCache<string>(cacheKey);
        if (cached is not null)
        {
            // Empty string indicates cached negative result (404)
            return string.IsNullOrEmpty(cached) ? null : cached;
        }

        var url500 = $"{CaaBaseUrl}/{releaseMbid}/front-500";
        var url250 = $"{CaaBaseUrl}/{releaseMbid}/front-250";

        // Check availability of front-500 first, falling back to front-250
        var resolvedUrl = await _pipelines.ExecuteWithFallbackAsync<string?>(
            ServiceProviderIds.MusicBrainz,
            async ct =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url500);
                return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            },
            async (response, ct) =>
            {
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.MovedPermanently)
                {
                    return url500;
                }

                // Try 250px fallback
                using var request250 = new HttpRequestMessage(HttpMethod.Head, url250);
                var response250 = await _httpClient.SendAsync(request250, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (response250.IsSuccessStatusCode || response250.StatusCode == HttpStatusCode.Redirect)
                {
                    return url250;
                }

                return null;
            },
            fallback: null,
            _logger,
            $"resolve cover art for release {releaseMbid}",
            cancellationToken).ConfigureAwait(false);

        // Cache result (even null as empty string to prevent repeated 404 checks)
        SaveToCache(cacheKey, resolvedUrl ?? string.Empty, resolvedUrl != null ? TimeSpan.FromDays(14) : TimeSpan.FromDays(2));
        return resolvedUrl;
    }

    #region Canonical Release Selection & Mapping

    private MusicBrainzRecordingDetail MapRecordingToDetail(
        MusicBrainzRecordingLookupResponse recording,
        string? preferredAlbum)
    {
        var artistName = FormatArtistCredit(recording.ArtistCredits);
        var primaryArtistId = recording.ArtistCredits?.FirstOrDefault()?.Artist?.Id;

        var selectedRelease = SelectCanonicalRelease(recording.Releases, preferredAlbum, recording.Title);

        int? trackNumber = null;
        int? totalTracks = null;
        int? discNumber = null;
        int? totalDiscs = null;
        string? label = null;
        string? albumTitle = selectedRelease?.Title;
        string? albumArtist = FormatArtistCredit(selectedRelease?.ArtistCredits) is { Length: > 0 } relArt ? relArt : artistName;
        int? year = null;
        string? releaseDate = selectedRelease?.Date;

        if (selectedRelease != null)
        {
            year = ParseYear(selectedRelease.Date);
            label = selectedRelease.LabelInfo?.FirstOrDefault()?.Label?.Name;

            if (selectedRelease.Media != null && selectedRelease.Media.Count > 0)
            {
                totalDiscs = selectedRelease.Media.Count;

                // Find the medium/disc containing this recording
                MusicBrainzMediaDto? matchedMedia = null;
                MusicBrainzTrackDto? matchedTrack = null;

                foreach (var medium in selectedRelease.Media)
                {
                    if (medium.Tracks != null)
                    {
                        var track = medium.Tracks.FirstOrDefault(t =>
                            string.Equals(t.Title, recording.Title, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t.Id, recording.Id, StringComparison.OrdinalIgnoreCase));

                        if (track != null)
                        {
                            matchedMedia = medium;
                            matchedTrack = track;
                            break;
                        }
                    }
                }

                matchedMedia ??= selectedRelease.Media[0];
                discNumber = matchedMedia.Position;
                totalTracks = matchedMedia.TrackCount > 0 ? matchedMedia.TrackCount : matchedMedia.Tracks?.Count;

                if (matchedTrack != null)
                {
                    trackNumber = int.TryParse(matchedTrack.Number, out var parsedNum) ? parsedNum : matchedTrack.Position;
                }
            }
        }

        var genres = recording.Tags?
            .OrderByDescending(t => t.Count)
            .Select(t => t.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList() ?? new List<string>();

        var isrc = recording.Isrcs?.FirstOrDefault();

        return new MusicBrainzRecordingDetail
        {
            RecordingId = recording.Id,
            Title = recording.Title,
            Artist = artistName,
            ArtistId = primaryArtistId,
            ReleaseId = selectedRelease?.Id,
            Album = albumTitle,
            AlbumArtist = albumArtist,
            Year = year,
            ReleaseDate = releaseDate,
            TrackNumber = trackNumber,
            TotalTracks = totalTracks,
            DiscNumber = discNumber,
            TotalDiscs = totalDiscs,
            Label = label,
            Isrc = isrc,
            Genres = genres,
            Score = 100
        };
    }

    /// <summary>
    ///     Applies the canonical release selection heuristic (FR-009):
    ///     1. If preferredAlbum matches a release title, prioritize it.
    ///     2. Prioritize status == "Official" and primary-type == "Album".
    ///     3. Avoid "Compilation" and "Live" unless no other official releases exist.
    ///     4. Pick the release with the oldest date (original release date).
    /// </summary>
    private MusicBrainzReleaseLookupDto? SelectCanonicalRelease(
        List<MusicBrainzReleaseLookupDto>? releases,
        string? preferredAlbum,
        string recordingTitle)
    {
        if (releases == null || releases.Count == 0)
            return null;

        // 1. Direct album title match if local album is known
        if (!string.IsNullOrWhiteSpace(preferredAlbum))
        {
            var titleMatch = releases.FirstOrDefault(r =>
                string.Equals(r.Title, preferredAlbum, StringComparison.OrdinalIgnoreCase));
            if (titleMatch != null)
                return titleMatch;
        }

        // 2. Score and sort releases
        var scored = releases.Select(r => new
        {
            Release = r,
            Score = CalculateReleaseScore(r),
            Date = ParseYear(r.Date) ?? 9999
        })
        .OrderByDescending(x => x.Score)
        .ThenBy(x => x.Date)
        .ToList();

        return scored.FirstOrDefault()?.Release;
    }

    private static int CalculateReleaseScore(MusicBrainzReleaseLookupDto release)
    {
        int score = 0;

        // Status Official is strongly preferred
        if (string.Equals(release.Status, "Official", StringComparison.OrdinalIgnoreCase))
            score += 100;

        var primaryType = release.ReleaseGroup?.PrimaryType;
        if (string.Equals(primaryType, "Album", StringComparison.OrdinalIgnoreCase))
            score += 50;
        else if (string.Equals(primaryType, "EP", StringComparison.OrdinalIgnoreCase))
            score += 20;
        else if (string.Equals(primaryType, "Single", StringComparison.OrdinalIgnoreCase))
            score += 10;

        // Penalize compilations, live albums, remasters unless that's all there is
        var secondaryTypes = release.ReleaseGroup?.SecondaryTypes;
        if (secondaryTypes != null)
        {
            if (secondaryTypes.Any(t => string.Equals(t, "Compilation", StringComparison.OrdinalIgnoreCase)))
                score -= 40;
            if (secondaryTypes.Any(t => string.Equals(t, "Live", StringComparison.OrdinalIgnoreCase)))
                score -= 30;
        }

        // Bonus if release has complete media & track information
        if (release.Media != null && release.Media.Count > 0)
            score += 10;

        return score;
    }

    private static string FormatArtistCredit(List<MusicBrainzArtistCreditDto>? credits)
    {
        if (credits == null || credits.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var credit in credits)
        {
            sb.Append(credit.Name ?? credit.Artist?.Name ?? string.Empty);
            if (!string.IsNullOrEmpty(credit.JoinPhrase))
            {
                sb.Append(credit.JoinPhrase);
            }
        }

        return sb.ToString().Trim();
    }

    private static int? ParseYear(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        // Dates are typically "YYYY-MM-DD", "YYYY-MM", or "YYYY"
        if (dateString.Length >= 4 && int.TryParse(dateString.AsSpan(0, 4), out var year) && year > 1800 && year < 2100)
        {
            return year;
        }

        return null;
    }

    private static string EscapeLucene(string input)
    {
        var sb = new StringBuilder();
        foreach (var c in input)
        {
            if (c is '+' or '-' or '&' or '|' or '!' or '(' or ')' or '{' or '}' or '[' or ']' or '^' or '"' or '~' or '*' or '?' or ':' or '\\' or '/')
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    #endregion

    #region Disk Cache Management (FR-011)

    private sealed class CacheEnvelope<T>
    {
        [JsonPropertyName("cachedAt")]
        public DateTimeOffset CachedAt { get; set; }

        [JsonPropertyName("expiresAt")]
        public DateTimeOffset ExpiresAt { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    private T? TryGetFromCache<T>(string key)
    {
        try
        {
            var filePath = GetCacheFilePath(key);
            if (!File.Exists(filePath))
                return default;

            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(filePath) > TimeSpan.FromDays(7))
            {
                try { File.Delete(filePath); } catch { /* ignore */ }
                return default;
            }

            var json = File.ReadAllText(filePath);
            var envelope = JsonSerializer.Deserialize<CacheEnvelope<T>>(json);
            if (envelope == null || DateTimeOffset.UtcNow > envelope.ExpiresAt)
            {
                try { File.Delete(filePath); } catch { /* ignore */ }
                return default;
            }

            return envelope.Data;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read cache for key {Key}", key);
            return default;
        }
    }

    private void SaveToCache<T>(string key, T data, TimeSpan? ttl = null)
    {
        try
        {
            var filePath = GetCacheFilePath(key);
            var envelope = new CacheEnvelope<T>
            {
                CachedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(ttl ?? TimeSpan.FromDays(7)),
                Data = data
            };

            var json = JsonSerializer.Serialize(envelope);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to write cache for key {Key}", key);
        }
    }

    private string GetCacheFilePath(string key)
    {
        var safeFileName = $"{ComputeHash(key)}.json";
        return Path.Combine(_cacheDirectory, safeFileName);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static readonly HashSet<string> UnknownArtistNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Desconhecido Artista",
        "Artista Desconhecido",
        "Desconhecido",
        "Unknown Artist",
        "Unknown",
        "—",
        "-",
        "Various Artists",
        "Vários Artistas"
    };

    private static bool IsUnknownArtist(string? artist)
    {
        return string.IsNullOrWhiteSpace(artist) || UnknownArtistNames.Contains(artist.Trim());
    }

    #endregion

    // DTO for SearchArtistAsync
    private sealed class MusicBrainzArtistSearchResult
    {
        [JsonPropertyName("artists")]
        public List<MusicBrainzArtist>? Artists { get; set; }
    }

    private sealed class MusicBrainzArtist
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
