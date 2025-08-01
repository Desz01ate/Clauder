namespace Clauder.Models;

using System.Text.Json.Serialization;

public sealed record Message
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }
}

public sealed record ClaudeSessionMetadata
{
    [JsonPropertyName("parentUuid")]
    public object ParentUuid { get; init; }

    [JsonPropertyName("isSidechain")]
    public bool IsSidechain { get; init; }

    [JsonPropertyName("userType")]
    public string UserType { get; init; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("gitBranch")]
    public string? GitBranch { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("message")]
    public Message? Message { get; init; }

    [JsonPropertyName("isMeta")]
    public bool IsMeta { get; init; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }
}