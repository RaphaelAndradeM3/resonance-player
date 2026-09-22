using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Resonance.Core.Helpers;
using Resonance.Core.Http.MusicBrainz;
using Resonance.Core.Http.Pipelines;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;
using Resonance.Core.Services.Implementations;
using Resonance.Core.Tests.Utils;
using NSubstitute;
using Xunit;

namespace Resonance.Core.Tests;

/// <summary>
///     Comprehensive tests for the MusicBrainzService covering artist lookup,
///     recording lookup, canonical release selection heuristics, textual search,
///     Cover Art Archive resolution, disk caching, rate limiting, and error handling.
/// </summary>
public class MusicBrainzServiceTests : IDisposable
{
    private readonly TestHttpMessageHandler _httpHandler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MusicBrainzService> _logger;
    private readonly MusicBrainzService _service;
    private readonly List<ProviderPipelineProvider> _pipelinesToDispose = new();
    private readonly List<string> _tempDirsToDelete = new();

    public MusicBrainzServiceTests()
    {
        _httpHandler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(_httpHandler) { BaseAddress = new Uri("https://musicbrainz.org") };
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        _logger = Substitute.For<ILogger<MusicBrainzService>>();

        // Default service uses a fast, no-retry pipeline so happy-path tests don't pay
        // real-world rate-limit or backoff delays.
        _service = BuildService(permitsPerWindow: 1000, retries: 0);
    }

    private MusicBrainzService BuildService(
        int permitsPerWindow,
        int retries,
        TimeSpan? window = null,
        IPathConfiguration? pathConfig = null)
    {
        var pipelines = new ProviderPipelineProvider(
            new[]
            {
                new ProviderPolicy
                {
                    ProviderId = ServiceProviderIds.MusicBrainz,
                    Channel = new ChannelPolicy
                    {
                        PermitsPerWindow = permitsPerWindow,
                        Window = window ?? TimeSpan.FromSeconds(1),
                        MaxConcurrent = 4,
                        MaxRetries = retries,
                        BaseRetryDelay = TimeSpan.FromMilliseconds(20),
                        MaxRetryDelay = TimeSpan.FromMilliseconds(100),
                    },
                },
            },
            NullLogger<ProviderPipelineProvider>.Instance);
        _pipelinesToDispose.Add(pipelines);
        return new MusicBrainzService(_httpClientFactory, pipelines, _logger, pathConfig);
    }

    private (MusicBrainzService Service, string CacheDir) CreateServiceWithTempCache()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "resonance_mb_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _tempDirsToDelete.Add(tempDir);

        var pathConfig = Substitute.For<IPathConfiguration>();
        pathConfig.MetadataCachePath.Returns(tempDir);

        var service = BuildService(1000, 0, pathConfig: pathConfig);
        return (service, tempDir);
    }

    public void Dispose()
    {
        foreach (var p in _pipelinesToDispose) p.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _httpHandler.Dispose();
        foreach (var dir in _tempDirsToDelete)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
                // Best-effort cleanup in test teardown
            }
        }
        GC.SuppressFinalize(this);
    }

    #region Successful Lookup Tests

    [Fact]
    public async Task SearchArtistAsync_WithValidArtist_ReturnsMusicBrainzId()
    {
        // Arrange
        var mbResponse = new
        {
            artists = new[]
            {
                new { id = "b10bbbfc-cf9e-42e0-be17-e2c3e1d2600d", name = "The Beatles", score = 100 }
            }
        };
        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(mbResponse))
        });

        // Act
        var result = await _service.SearchArtistAsync("The Beatles");

        // Assert
        result.Should().Be("b10bbbfc-cf9e-42e0-be17-e2c3e1d2600d");
    }

    [Fact]
    public async Task SearchArtistAsync_WithLowScoreResult_ReturnsNull()
    {
        // Arrange
        var mbResponse = new
        {
            artists = new[]
            {
                new { id = "some-id", name = "Similar Artist", score = 50 } // Below 80 threshold
            }
        };
        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(mbResponse))
        });

        // Act
        var result = await _service.SearchArtistAsync("The Beatles");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchArtistAsync_WithNoResults_ReturnsNull()
    {
        // Arrange
        var mbResponse = new { artists = Array.Empty<object>() };
        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(mbResponse))
        });

        // Act
        var result = await _service.SearchArtistAsync("NonExistent Artist XYZ123");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Lucene Query Syntax Tests

    [Fact]
    public async Task SearchArtistAsync_WithMultiWordArtist_QuotesNameInQuery()
    {
        // Arrange
        string? capturedUrl = null;
        _httpHandler.SendAsyncFunc = (request, _) =>
        {
            capturedUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"artists\": []}")
            });
        };

        // Act
        await _service.SearchArtistAsync("The Rolling Stones");

        // Assert - URL should contain quoted artist name for Lucene (may be decoded in Uri.ToString())
        capturedUrl.Should().Contain("\"The Rolling Stones\"");
    }

    [Fact]
    public async Task SearchArtistAsync_WithSingleWordArtist_StillQuotesName()
    {
        // Arrange
        string? capturedUrl = null;
        _httpHandler.SendAsyncFunc = (request, _) =>
        {
            capturedUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"artists\": []}")
            });
        };

        // Act
        await _service.SearchArtistAsync("Madonna");

        // Assert - Even single word should be quoted for consistency (may be decoded in Uri.ToString())
        capturedUrl.Should().Contain("\"Madonna\"");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SearchArtistAsync_When503ServiceUnavailable_ReturnsNull()
    {
        // Arrange
        _httpHandler.SendAsyncFunc = (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        // Act
        var result = await _service.SearchArtistAsync("Any Artist");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchArtistAsync_WhenOtherHttpError_ReturnsNull()
    {
        // Arrange
        _httpHandler.SendAsyncFunc = (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Act
        var result = await _service.SearchArtistAsync("Any Artist");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchArtistAsync_WithEmptyArtistName_ReturnsNull()
    {
        // Act
        var result = await _service.SearchArtistAsync("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchArtistAsync_WithWhitespaceArtistName_ReturnsNull()
    {
        // Act
        var result = await _service.SearchArtistAsync("   ");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task SearchArtistAsync_WhenCancelled_ThrowsTaskCancelledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"artists\": [{\"id\": \"test-id\", \"score\": 100}]}")
        });

        // Act & Assert - Cancelled token should throw
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.SearchArtistAsync("Artist", cts.Token));
    }

    [Fact]
    public async Task SearchArtistAsync_WhenCancelledDuringDelay_ThrowsTaskCancelledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        _httpHandler.SendAsyncFunc = async (_, ct) =>
        {
            // Simulate the rate limit delay being cancelled
            cts.CancelAfter(10);
            await Task.Delay(1000, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        // Act & Assert - Cancelled token during delay should throw
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.SearchArtistAsync("Artist", cts.Token));
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task SearchArtistAsync_MultipleRapidCalls_RespectRateLimit()
    {
        // Arrange — 1 permit per 1s, no retries; verifies the pipeline rate limiter is wired.
        var service = BuildService(permitsPerWindow: 1, retries: 0);
        var callTimestamps = new List<DateTime>();
        _httpHandler.SendAsyncFunc = (_, _) =>
        {
            callTimestamps.Add(DateTime.UtcNow);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"artists\": [{\"id\": \"test-id\", \"name\": \"Artist\", \"score\": 100}]}")
            });
        };

        // Act - Make two rapid calls
        var task1 = service.SearchArtistAsync("Artist 1");
        var task2 = service.SearchArtistAsync("Artist 2");
        await Task.WhenAll(task1, task2);

        // Assert - Second call should be delayed by at least ~1 second
        callTimestamps.Should().HaveCount(2);
        var timeDiff = callTimestamps[1] - callTimestamps[0];
        timeDiff.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(900);
    }

    #endregion

    #region Recording Lookup Tests

    [Fact]
    public async Task GetRecordingMetadataAsync_WithValidMbid_ReturnsCanonicalDetails()
    {
        // Arrange
        var mbRecording = new MusicBrainzRecordingLookupResponse
        {
            Id = "rec-123",
            Title = "Karma Police",
            ArtistCredits = new()
            {
                new()
                {
                    Name = "Radiohead",
                    Artist = new() { Id = "art-456", Name = "Radiohead" }
                }
            },
            Releases = new()
            {
                new()
                {
                    Id = "rel-789",
                    Title = "OK Computer",
                    Status = "Official",
                    Date = "1997-05-21",
                    ReleaseGroup = new()
                    {
                        PrimaryType = "Album",
                        SecondaryTypes = new()
                    },
                    ArtistCredits = new() { new() { Name = "Radiohead" } },
                    LabelInfo = new()
                    {
                        new() { Label = new() { Name = "Parlophone" } }
                    },
                    Media = new()
                    {
                        new()
                        {
                            Position = 1,
                            TrackCount = 12,
                            Tracks = new()
                            {
                                new()
                                {
                                    Id = "rec-123",
                                    Title = "Karma Police",
                                    Number = "6",
                                    Position = 6
                                }
                            }
                        }
                    }
                }
            },
            Isrcs = new() { "GBAYE9700078" },
            Tags = new()
            {
                new() { Name = "alternative rock", Count = 25 },
                new() { Name = "art rock", Count = 10 }
            }
        };

        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(mbRecording))
        });

        // Act
        var result = await _service.GetRecordingMetadataAsync("rec-123");

        // Assert
        result.Should().NotBeNull();
        result!.RecordingId.Should().Be("rec-123");
        result.Title.Should().Be("Karma Police");
        result.Artist.Should().Be("Radiohead");
        result.ArtistId.Should().Be("art-456");
        result.ReleaseId.Should().Be("rel-789");
        result.Album.Should().Be("OK Computer");
        result.AlbumArtist.Should().Be("Radiohead");
        result.Year.Should().Be(1997);
        result.ReleaseDate.Should().Be("1997-05-21");
        result.TrackNumber.Should().Be(6);
        result.TotalTracks.Should().Be(12);
        result.DiscNumber.Should().Be(1);
        result.TotalDiscs.Should().Be(1);
        result.Label.Should().Be("Parlophone");
        result.Isrc.Should().Be("GBAYE9700078");
        result.Genres.Should().ContainInOrder("alternative rock", "art rock");
    }

    [Fact]
    public async Task GetRecordingMetadataAsync_CanonicalReleaseSelection_SelectsOfficialStudioAlbumOverCompilationAndLive()
    {
        // Arrange - Setup recording appearing on Compilation, Live, Promotion, and original studio album
        var mbRecording = new MusicBrainzRecordingLookupResponse
        {
            Id = "rec-bends",
            Title = "High and Dry",
            ArtistCredits = new() { new() { Name = "Radiohead" } },
            Releases = new()
            {
                new()
                {
                    Id = "rel-comp",
                    Title = "Greatest Hits",
                    Status = "Official",
                    Date = "2005-01-01",
                    ReleaseGroup = new()
                    {
                        PrimaryType = "Album",
                        SecondaryTypes = new() { "Compilation" }
                    },
                    Media = new() { new() { Position = 1, TrackCount = 18, Tracks = new() { new() { Title = "High and Dry", Number = "1", Position = 1 } } } }
                },
                new()
                {
                    Id = "rel-live",
                    Title = "Live in London",
                    Status = "Official",
                    Date = "2000-01-01",
                    ReleaseGroup = new()
                    {
                        PrimaryType = "Album",
                        SecondaryTypes = new() { "Live" }
                    },
                    Media = new() { new() { Position = 1, TrackCount = 14, Tracks = new() { new() { Title = "High and Dry", Number = "3", Position = 3 } } } }
                },
                new()
                {
                    Id = "rel-bootleg",
                    Title = "Radio Broadcast",
                    Status = "Promotion",
                    Date = "1994-01-01",
                    ReleaseGroup = new()
                    {
                        PrimaryType = "Album",
                        SecondaryTypes = new()
                    },
                    Media = new() { new() { Position = 1, TrackCount = 10, Tracks = new() { new() { Title = "High and Dry", Number = "2", Position = 2 } } } }
                },
                new()
                {
                    Id = "rel-studio",
                    Title = "The Bends",
                    Status = "Official",
                    Date = "1995-03-13",
                    ReleaseGroup = new()
                    {
                        PrimaryType = "Album",
                        SecondaryTypes = new()
                    },
                    Media = new() { new() { Position = 1, TrackCount = 12, Tracks = new() { new() { Title = "High and Dry", Number = "3", Position = 3 } } } }
                },
                new()
                {
                    Id = "rel-reissue",
                    Title = "The Bends (Collector's Edition)",
                    Status = "Official",
                    Date = "2009-03-23",
                    ReleaseGroup = new()
                    {
                        PrimaryType = "Album",
                        SecondaryTypes = new()
                    },
                    Media = new() { new() { Position = 1, TrackCount = 12, Tracks = new() { new() { Title = "High and Dry", Number = "3", Position = 3 } } } }
                }
            }
        };

        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(mbRecording))
        });

        // Act
        var result = await _service.GetRecordingMetadataAsync("rec-bends");

        // Assert - Heuristic picks "The Bends" (Official + Album + oldest date 1995 over 2009 reissue, compilation and live)
        result.Should().NotBeNull();
        result!.ReleaseId.Should().Be("rel-studio");
        result.Album.Should().Be("The Bends");
        result.Year.Should().Be(1995);
    }

    [Fact]
    public async Task GetRecordingMetadataAsync_WithPreferredAlbum_PrioritizesMatchingAlbum()
    {
        // Arrange
        var mbRecording = new MusicBrainzRecordingLookupResponse
        {
            Id = "rec-karma",
            Title = "Karma Police",
            ArtistCredits = new() { new() { Name = "Radiohead" } },
            Releases = new()
            {
                new()
                {
                    Id = "rel-orig",
                    Title = "OK Computer",
                    Status = "Official",
                    Date = "1997-05-21",
                    ReleaseGroup = new() { PrimaryType = "Album" },
                    Media = new() { new() { Position = 1, TrackCount = 12, Tracks = new() { new() { Title = "Karma Police", Number = "6", Position = 6 } } } }
                },
                new()
                {
                    Id = "rel-anniv",
                    Title = "OK Computer OKNOTOK 1997 2017",
                    Status = "Official",
                    Date = "2017-06-23",
                    ReleaseGroup = new() { PrimaryType = "Album" },
                    Media = new() { new() { Position = 1, TrackCount = 23, Tracks = new() { new() { Title = "Karma Police", Number = "6", Position = 6 } } } }
                }
            }
        };

        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(mbRecording))
        });

        // Act - Specify preferred album matching the 2017 edition
        var result = await _service.GetRecordingMetadataAsync("rec-karma", preferredAlbum: "OK Computer OKNOTOK 1997 2017");

        // Assert
        result.Should().NotBeNull();
        result!.ReleaseId.Should().Be("rel-anniv");
        result.Album.Should().Be("OK Computer OKNOTOK 1997 2017");
        result.Year.Should().Be(2017);
    }

    [Fact]
    public async Task GetRecordingMetadataAsync_WithEmptyMbid_ReturnsNull()
    {
        var result = await _service.GetRecordingMetadataAsync("");
        result.Should().BeNull();
    }

    #endregion

    #region Textual Search Tests

    [Fact]
    public async Task SearchRecordingAsync_WithValidArtistAndTitle_FindsMatchAndFetchesFullDetail()
    {
        // Arrange
        var searchResponse = new MusicBrainzRecordingSearchResponse
        {
            Recordings = new()
            {
                new()
                {
                    Id = "rec-search-1",
                    Score = 95,
                    Title = "No Surprises"
                }
            }
        };

        var detailResponse = new MusicBrainzRecordingLookupResponse
        {
            Id = "rec-search-1",
            Title = "No Surprises",
            ArtistCredits = new() { new() { Name = "Radiohead" } },
            Releases = new()
            {
                new()
                {
                    Id = "rel-okc",
                    Title = "OK Computer",
                    Status = "Official",
                    Date = "1997-05-21",
                    ReleaseGroup = new() { PrimaryType = "Album" },
                    Media = new() { new() { Position = 1, TrackCount = 12, Tracks = new() { new() { Title = "No Surprises", Number = "10", Position = 10 } } } }
                }
            }
        };

        _httpHandler.SendAsyncFunc = (request, _) =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("/recording?query="))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(searchResponse))
                });
            }

            if (url.Contains("/recording/rec-search-1"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(detailResponse))
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        };

        // Act
        var result = await _service.SearchRecordingAsync("Radiohead", "No Surprises");

        // Assert
        result.Should().NotBeNull();
        result!.RecordingId.Should().Be("rec-search-1");
        result.Title.Should().Be("No Surprises");
        result.Album.Should().Be("OK Computer");
        result.Year.Should().Be(1997);
    }

    [Fact]
    public async Task SearchRecordingAsync_WithLowScore_ReturnsNull()
    {
        // Arrange
        var searchResponse = new MusicBrainzRecordingSearchResponse
        {
            Recordings = new()
            {
                new()
                {
                    Id = "rec-low",
                    Score = 45, // Below 60 threshold
                    Title = "Vaguely Similar Track"
                }
            }
        };

        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(searchResponse))
        });

        // Act
        var result = await _service.SearchRecordingAsync("Radiohead", "Unknown Song");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchRecordingAsync_WithEmptyInputs_ReturnsNull()
    {
        var result1 = await _service.SearchRecordingAsync("", "Title");
        var result2 = await _service.SearchRecordingAsync("Artist", "");
        result1.Should().BeNull();
        result2.Should().BeNull();
    }

    #endregion

    #region Cover Art Archive Tests

    [Fact]
    public async Task GetCoverArtUrlAsync_When500Exists_ReturnsFront500Url()
    {
        // Arrange
        _httpHandler.SendAsyncFunc = (request, _) =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.EndsWith("/front-500"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        };

        // Act
        var result = await _service.GetCoverArtUrlAsync("rel-art-1");

        // Assert
        result.Should().Be("https://coverartarchive.org/release/rel-art-1/front-500");
    }

    [Fact]
    public async Task GetCoverArtUrlAsync_When500NotFound_FallsBackTo250()
    {
        // Arrange
        _httpHandler.SendAsyncFunc = (request, _) =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.EndsWith("/front-500"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            if (url.EndsWith("/front-250"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        };

        // Act
        var result = await _service.GetCoverArtUrlAsync("rel-art-2");

        // Assert
        result.Should().Be("https://coverartarchive.org/release/rel-art-2/front-250");
    }

    [Fact]
    public async Task GetCoverArtUrlAsync_WhenBothNotFound_ReturnsNull()
    {
        // Arrange
        _httpHandler.SendAsyncFunc = (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var result = await _service.GetCoverArtUrlAsync("rel-art-none");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCoverArtUrlAsync_WithEmptyReleaseMbid_ReturnsNull()
    {
        var result = await _service.GetCoverArtUrlAsync("");
        result.Should().BeNull();
    }

    #endregion

    #region Disk Caching Tests

    [Fact]
    public async Task GetRecordingMetadataAsync_CachesResultOnDisk_SecondCallUsesCache()
    {
        // Arrange
        var (service, cacheDir) = CreateServiceWithTempCache();
        var mbRecording = new MusicBrainzRecordingLookupResponse
        {
            Id = "rec-cached-1",
            Title = "Paranoid Android",
            ArtistCredits = new() { new() { Name = "Radiohead" } }
        };

        var httpCallCount = 0;
        _httpHandler.SendAsyncFunc = (_, _) =>
        {
            httpCallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(mbRecording))
            });
        };

        // Act - First call populates cache
        var firstResult = await service.GetRecordingMetadataAsync("rec-cached-1");

        // Assert - First call hit HTTP and wrote cache file
        firstResult.Should().NotBeNull();
        httpCallCount.Should().Be(1);
        Directory.GetFiles(cacheDir, "*.json").Should().NotBeEmpty();

        // Simulate network failure for second call
        _httpHandler.SendAsyncFunc = (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Act - Second call
        var secondResult = await service.GetRecordingMetadataAsync("rec-cached-1");

        // Assert - Second call succeeded using disk cache
        secondResult.Should().NotBeNull();
        secondResult!.RecordingId.Should().Be("rec-cached-1");
        secondResult.Title.Should().Be("Paranoid Android");
        httpCallCount.Should().Be(1); // No new HTTP calls were made
    }

    [Fact]
    public async Task GetRecordingMetadataAsync_ExpiredCache_RefetchesFromNetwork()
    {
        // Arrange
        var (service, cacheDir) = CreateServiceWithTempCache();

        var mbRecording1 = new MusicBrainzRecordingLookupResponse
        {
            Id = "rec-exp-1",
            Title = "Initial Title",
            ArtistCredits = new() { new() { Name = "Radiohead" } }
        };

        var mbRecording2 = new MusicBrainzRecordingLookupResponse
        {
            Id = "rec-exp-1",
            Title = "Refetched Title",
            ArtistCredits = new() { new() { Name = "Radiohead" } }
        };

        var currentResponse = mbRecording1;
        _httpHandler.SendAsyncFunc = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(currentResponse))
        });

        // First call populates cache
        var result1 = await service.GetRecordingMetadataAsync("rec-exp-1");
        result1!.Title.Should().Be("Initial Title");

        // Expire all cache files on disk by setting LastWriteTimeUtc > 7 days ago
        var cacheFiles = Directory.GetFiles(cacheDir, "*.json");
        foreach (var file in cacheFiles)
        {
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));
        }

        // Update server response
        currentResponse = mbRecording2;

        // Act - Second call should detect expiration and refetch
        var result2 = await service.GetRecordingMetadataAsync("rec-exp-1");

        // Assert
        result2.Should().NotBeNull();
        result2!.Title.Should().Be("Refetched Title");
    }

    #endregion

    #region Header Validation Tests

    [Fact]
    public async Task MusicBrainzRequests_IncludeOfficialUserAgentAndAcceptJson()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _httpHandler.SendAsyncFunc = (req, _) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"artists\": []}")
            });
        };

        // Act
        await _service.SearchArtistAsync("Radiohead");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.UserAgent.ToString().Should().Contain("Resonance/1.0");
        capturedRequest.Headers.Accept.ToString().Should().Contain("application/json");
    }

    #endregion
}
