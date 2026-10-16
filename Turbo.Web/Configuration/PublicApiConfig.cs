namespace Turbo.Web.Configuration;

/// <summary>
/// The public API's room endpoints (<c>Turbo:Web:PublicApi</c>): a room, and the rooms a player
/// owns, as Habbo's OpenAPI document has them (<c>/api/public/rooms/{roomId}</c>,
/// <c>/api/public/users/{id}/rooms</c>), so a client library written for Habbo works with this
/// hotel's address.
/// </summary>
public sealed class PublicApiConfig
{
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// A room's thumbnail, with <c>{roomId}</c> for its id, such as the navigator's thumbnail
    /// address. Empty leaves <c>thumbnailUrl</c> out.
    /// </summary>
    public string RoomThumbnailUrl { get; init; } = "";

    /// <summary>A room's full-size picture, with <c>{roomId}</c>. Empty leaves <c>imageUrl</c> out.</summary>
    public string RoomImageUrl { get; init; } = "";
}
