using System.Text.Json.Serialization;

namespace Turbo.Web.Api.Contracts;

/// <summary>
/// A room as the public API shows it (Habbo's OpenAPI document, <c>GET /api/public/rooms/{roomId}</c>).
/// This hotel has no unique ids, so a room's and its owner's are their ids (inference); a field
/// with nothing to say is left out.
/// </summary>
public sealed record PublicRoom(
    int Id,
    string Name,
    string Description,
    string CreationTime,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? HabboGroupId,
    string[] Tags,
    int MaximumVisitors,
    bool ShowOwnerName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OwnerName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OwnerUniqueId,
    string[] Categories,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ThumbnailUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ImageUrl,
    int Rating,
    string UniqueId
);
