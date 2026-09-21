using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Resonance.Core.Http.AcoustId;
using Resonance.Core.Http.Pipelines;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;
using Resonance.Core.Services.Implementations;
using Resonance.Core.Tests.Utils;
using Xunit;

namespace Resonance.Core.Tests.Services;

/// <summary>
///     Testes unitários para AcoustIdService cobrindo sucesso, mapeamento de candidatos,
///     filtro de corte de 40%, destaque para >= 80%, rate limit, cache, inspeção de payload e privacidade.
/// </summary>
public class AcoustIdServiceTests : IDisposable
{
    private readonly TestHttpMessageHandler _httpHandler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApiKeyService _apiKeyService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<AcoustIdService> _logger;
    private readonly List<ProviderPipelineProvider> _pipelinesToDispose = new();

    public AcoustIdServiceTests()
    {
        _httpHandler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(_httpHandler);
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _apiKeyService = Substitute.For<IApiKeyService>();
        _apiKeyService.GetApiKeyAsync(ServiceProviderIds.AcoustId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("test-app-key"));

        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.GetFetchOnlineMetadataEnabledAsync().Returns(Task.FromResult(true));
        _settingsService.GetEnabledServiceProvidersAsync(ServiceCategory.Metadata)
            .Returns(Task.FromResult(new List<ServiceProviderSetting>
            {
                new() { Id = ServiceProviderIds.AcoustId, IsEnabled = true }
            }));
        _settingsService.GetAcoustIdUserApiKeyAsync().Returns(Task.FromResult(string.Empty));

        _logger = Substitute.For<ILogger<AcoustIdService>>();
    }

    private AcoustIdService BuildService(int permitsPerWindow = 1000, int retries = 0)
    {
        var pipelines = new ProviderPipelineProvider(
            new[]
            {
                new ProviderPolicy
                {
                    ProviderId = ServiceProviderIds.AcoustId,
                    Channel = new ChannelPolicy
                    {
                        PermitsPerWindow = permitsPerWindow,
                        Window = TimeSpan.FromSeconds(1),
                        MaxConcurrent = 2,
                        MaxRetries = retries,
                        BaseRetryDelay = TimeSpan.FromMilliseconds(20),
                        MaxRetryDelay = TimeSpan.FromMilliseconds(100),
                    },
                },
            },
            NullLogger<ProviderPipelineProvider>.Instance);

        _pipelinesToDispose.Add(pipelines);
        return new AcoustIdService(_httpClientFactory, pipelines, _apiKeyService, _settingsService, _logger);
    }

    public void Dispose()
    {
        foreach (var p in _pipelinesToDispose)
        {
            p.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        _httpHandler.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task LookupAsync_WithValidFingerprint_ReturnsSuccessAndRankedCandidates()
    {
        // Arrange
        var service = BuildService();
        var fakeResponse = new AcoustIdLookupResponse
        {
            Status = "ok",
            Results = new List<AcoustIdLookupResult>
            {
                new()
                {
                    Id = "acoustid-111",
                    Score = 0.85,
                    Recordings = new List<AcoustIdRecording>
                    {
                        new()
                        {
                            Id = "mb-rec-1",
                            Title = "Bohemian Rhapsody",
                            Artists = new List<AcoustIdArtist> { new() { Id = "mb-art-1", Name = "Queen" } },
                            ReleaseGroups = new List<AcoustIdReleaseGroup> { new() { Id = "mb-rel-1", Title = "A Night at the Opera" } }
                        }
                    }
                },
                new()
                {
                    Id = "acoustid-222",
                    Score = 0.65,
                    Recordings = new List<AcoustIdRecording>
                    {
                        new()
                        {
                            Id = "mb-rec-2",
                            Title = "Bohemian Rhapsody (Live)",
                            Artists = new List<AcoustIdArtist> { new() { Id = "mb-art-1", Name = "Queen" } }
                        }
                    }
                }
            }
        };

        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(fakeResponse))
        });

        var fingerprint = new AcousticFingerprint("AQAA51EkJUmSJR...validhash", 120);

        // Act
        var result = await service.LookupAsync(fingerprint);

        // Assert
        result.Status.Should().Be(RecognitionStatus.Success);
        result.Candidates.Should().HaveCount(2);

        var first = result.Candidates[0];
        first.Title.Should().Be("Bohemian Rhapsody");
        first.Artist.Should().Be("Queen");
        first.Album.Should().Be("A Night at the Opera");
        first.AcoustId.Should().Be("acoustid-111");
        first.MusicBrainzTrackId.Should().Be("mb-rec-1");
        first.MusicBrainzReleaseId.Should().Be("mb-rel-1");
        first.MusicBrainzArtistId.Should().Be("mb-art-1");
        first.ConfidenceScore.Should().Be(0.85);
        first.ConfidencePercentage.Should().Be(85);
        first.IsHighConfidence.Should().BeTrue();

        var second = result.Candidates[1];
        second.ConfidenceScore.Should().Be(0.65);
        second.ConfidencePercentage.Should().Be(65);
        second.IsHighConfidence.Should().BeFalse();
    }

    [Fact]
    public async Task LookupAsync_FiltersOutScoresBelow40Percent()
    {
        // Arrange
        var service = BuildService();
        var fakeResponse = new AcoustIdLookupResponse
        {
            Status = "ok",
            Results = new List<AcoustIdLookupResult>
            {
                new()
                {
                    Id = "acoustid-low",
                    Score = 0.35, // Below 0.40 cutoff
                    Recordings = new List<AcoustIdRecording>
                    {
                        new() { Id = "mb-rec-low", Title = "Low Score Track" }
                    }
                },
                new()
                {
                    Id = "acoustid-ok",
                    Score = 0.50,
                    Recordings = new List<AcoustIdRecording>
                    {
                        new() { Id = "mb-rec-ok", Title = "Valid Score Track" }
                    }
                }
            }
        };

        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(fakeResponse))
        });

        var fingerprint = new AcousticFingerprint("AQAAvalidhash...", 120);

        // Act
        var result = await service.LookupAsync(fingerprint);

        // Assert
        result.Status.Should().Be(RecognitionStatus.Success);
        result.Candidates.Should().HaveCount(1);
        result.Candidates[0].Title.Should().Be("Valid Score Track");
    }

    [Fact]
    public async Task LookupAsync_WhenAllScoresBelowCutoff_ReturnsNoMatchFound()
    {
        // Arrange
        var service = BuildService();
        var fakeResponse = new AcoustIdLookupResponse
        {
            Status = "ok",
            Results = new List<AcoustIdLookupResult>
            {
                new()
                {
                    Id = "acoustid-low",
                    Score = 0.25,
                    Recordings = new List<AcoustIdRecording>
                    {
                        new() { Id = "mb-rec-low", Title = "Too Low" }
                    }
                }
            }
        };

        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(fakeResponse))
        });

        var fingerprint = new AcousticFingerprint("AQAAvalidhash...", 60);

        // Act
        var result = await service.LookupAsync(fingerprint);

        // Assert
        result.Status.Should().Be(RecognitionStatus.NoMatchFound);
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_WhenAudioShorterThan10Seconds_ReturnsAudioTooShortWithoutHttpCall()
    {
        // Arrange
        var service = BuildService();
        var shortFingerprint = new AcousticFingerprint("AQAAshort...", 8);

        // Act
        var result = await service.LookupAsync(shortFingerprint);

        // Assert
        result.Status.Should().Be(RecognitionStatus.AudioTooShort);
        result.ErrorMessage.Should().Contain("10 segundos");
        _httpHandler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_WhenFingerprintInvalid_ReturnsAnalysisFailedWithoutHttpCall()
    {
        // Arrange
        var service = BuildService();
        var invalidFingerprint = AcousticFingerprint.Empty;

        // Act
        var result = await service.LookupAsync(invalidFingerprint);

        // Assert
        result.Status.Should().Be(RecognitionStatus.AnalysisFailed);
        _httpHandler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_WhenOnlineMetadataDisabled_ReturnsOfflineOrDisabledWithoutHttpCall()
    {
        // Arrange
        _settingsService.GetFetchOnlineMetadataEnabledAsync().Returns(Task.FromResult(false));
        var service = BuildService();
        var fingerprint = new AcousticFingerprint("AQAAvalidhash...", 90);

        // Act
        var result = await service.LookupAsync(fingerprint);

        // Assert
        result.Status.Should().Be(RecognitionStatus.OfflineOrDisabled);
        _httpHandler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_WhenCalledRepeatedly_ReturnsCachedResultWithoutDuplicateHttpCalls()
    {
        // Arrange
        var service = BuildService();
        var fakeResponse = new AcoustIdLookupResponse
        {
            Status = "ok",
            Results = new List<AcoustIdLookupResult>
            {
                new()
                {
                    Id = "acoustid-cached",
                    Score = 0.90,
                    Recordings = new List<AcoustIdRecording>
                    {
                        new() { Id = "mb-cached", Title = "Cached Track" }
                    }
                }
            }
        };

        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(fakeResponse))
        });

        var fingerprint = new AcousticFingerprint("AQAAsamehash...", 120);

        // Act - 1st call
        var firstResult = await service.LookupAsync(fingerprint);
        // Act - 2nd call with identical fingerprint
        var secondResult = await service.LookupAsync(fingerprint);

        // Assert
        firstResult.Status.Should().Be(RecognitionStatus.Success);
        secondResult.Status.Should().Be(RecognitionStatus.Success);
        secondResult.Candidates[0].Title.Should().Be("Cached Track");
        _httpHandler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task LookupAsync_InspectsRequest_EnsuresOnlyMetadataSentAndNoBinaryAudio()
    {
        // Arrange
        var service = BuildService();
        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new AcoustIdLookupResponse { Status = "ok", Results = new() }))
        });

        var fingerprint = new AcousticFingerprint("AQAAfingerprintstring123", 115);

        // Act
        await service.LookupAsync(fingerprint);

        // Assert
        _httpHandler.Requests.Should().HaveCount(1);
        var request = _httpHandler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.ToString().Should().Be("https://api.acoustid.org/v2/lookup");

        var body = await request.Content!.ReadAsStringAsync();
        body.Should().Contain("client=");
        body.Should().Contain("duration=115");
        body.Should().Contain("fingerprint=AQAAfingerprintstring123");
        body.Should().Contain("meta=recordings+releasegroups");

        // Principle IV verification: No raw audio stream or multipart audio bytes
        request.Content.Headers.ContentType!.MediaType.Should().Be("application/x-www-form-urlencoded");
    }

    [Fact]
    public async Task LookupAsync_WhenRateLimited429_ReturnsRateLimitedStatus()
    {
        // Arrange
        var service = BuildService(retries: 0);
        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("Too Many Requests")
        });

        var fingerprint = new AcousticFingerprint("AQAAvalidhash...", 100);

        // Act
        var result = await service.LookupAsync(fingerprint);

        // Assert
        result.Status.Should().Be(RecognitionStatus.RateLimited);
        result.ErrorMessage.Should().Contain("Limite de requisições");
    }

    [Fact]
    public async Task LookupAsync_WhenAcoustIdReturnsErrorJson_ReturnsAnalysisFailed()
    {
        // Arrange
        var service = BuildService();
        var errorResponse = new AcoustIdLookupResponse
        {
            Status = "error",
            Error = new AcoustIdError { Message = "invalid client key", Code = 4 }
        };

        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(errorResponse))
        });

        var fingerprint = new AcousticFingerprint("AQAAvalidhash...", 95);

        // Act
        var result = await service.LookupAsync(fingerprint);

        // Assert
        result.Status.Should().Be(RecognitionStatus.AnalysisFailed);
        result.ErrorMessage.Should().Be("invalid client key");
    }

    [Fact]
    public async Task LookupAsync_WithUserCustomApiKey_UsesCustomKeyInRequest()
    {
        // Arrange
        _settingsService.GetAcoustIdUserApiKeyAsync().Returns(Task.FromResult("custom-user-api-key"));
        var service = BuildService();
        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new AcoustIdLookupResponse { Status = "ok", Results = new() }))
        });

        var fingerprint = new AcousticFingerprint("AQAAvalidhash...", 80);

        // Act
        await service.LookupAsync(fingerprint);

        // Assert
        _httpHandler.Requests.Should().HaveCount(1);
        var body = await _httpHandler.Requests[0].Content!.ReadAsStringAsync();
        body.Should().Contain("client=custom-user-api-key");
    }
}
