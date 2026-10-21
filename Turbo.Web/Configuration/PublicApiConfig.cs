using Turbo.Primitives.Settings;

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
    /// address (Habbo's: <c>.../navigator-thumbnail/hhus/{roomId}.png</c>). Empty leaves
    /// <c>thumbnailUrl</c> out.
    /// </summary>
    [UrlSetting]
    public string RoomThumbnailUrl { get; init; } = "";

    /// <summary>
    /// A room's full-size picture, with <c>{roomId}</c> (Habbo's: <c>.../fullroom-photo/hhus/{roomId}.png</c>).
    /// Empty leaves <c>imageUrl</c> out.
    /// </summary>
    [UrlSetting]
    public string RoomImageUrl { get; init; } = "";

    /// <summary>
    /// Sites whose pages may read these endpoints from a browser (CORS): their origins
    /// (<c>https://example.com</c>), or <c>*</c> for any site, never with credentials. Any by
    /// default, as Habbo's: its API answered cross-origin requests for a room from any origin
    /// (browser check, 2026-10-20). An empty list allows none.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = ["*"];
}
