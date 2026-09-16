using System.Text.Json.Serialization;

namespace ChessApp.Backend.DTOs;

public class BlockUserRequest
{
    // Mobile sends { "userId": ..., "reason": ... } - map the wire name explicitly
    // since it differs from the C# property name.
    [JsonPropertyName("userId")]
    public Guid BlockedUserId { get; set; }
    public string? Reason { get; set; }
}

public class BlockDto
{
    public Guid Id { get; set; }
    public Guid BlockedUserId { get; set; }
    public string? BlockedUsername { get; set; }
    public DateTime BlockedAt { get; set; }
    public string? Reason { get; set; }
}

public class BlockListResponse
{
    public List<BlockDto> BlockedUsers { get; set; } = new();
    public int Count => BlockedUsers.Count;
}
