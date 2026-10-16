using System.Text.Json.Serialization;

namespace Turbo.Web.Api.Contracts;

/// <summary>
/// A room as the public API shows it (Habbo's OpenAPI document, <c>GET /api/public/rooms/{roomId}</c>),
/// in the order of an official client capture of that endpoint (2026-10-16), which also carries
/// <c>publicRoom</c> and <c>doorMode</c>. This hotel has no unique ids, so a room's and its
/// owner's are their ids (inference); a field with nothing to say is left out.
/// </summary>
public sealed record PublicApiRoom(
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
    bool PublicRoom,
    string DoorMode,
    string UniqueId
);
