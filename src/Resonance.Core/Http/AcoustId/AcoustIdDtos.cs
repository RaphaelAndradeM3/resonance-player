namespace Resonance.Core.Http.AcoustId;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public sealed class AcoustIdLookupResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("results")]
    public List<AcoustIdLookupResult>? Results { get; set; }

    [JsonPropertyName("error")]
    public AcoustIdError? Error { get; set; }
}

public sealed class AcoustIdLookupResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("recordings")]
    public List<AcoustIdRecording>? Recordings { get; set; }
}

public sealed class AcoustIdRecording
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("artists")]
    public List<AcoustIdArtist>? Artists { get; set; }

    [JsonPropertyName("releasegroups")]
    public List<AcoustIdReleaseGroup>? ReleaseGroups { get; set; }
}

public sealed class AcoustIdArtist
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class AcoustIdReleaseGroup
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("artists")]
    public List<AcoustIdArtist>? Artists { get; set; }
}

public sealed class AcoustIdError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public int Code { get; set; }
}
