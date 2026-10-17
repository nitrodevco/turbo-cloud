namespace Turbo.Primitives.Rooms;

/// <summary>
/// The rooms' thumbnails as the room thumbnail camera drew them: the picture the navigator shows
/// for a room (Habbo's <c>navigator.thumbnail.url_base</c> + <c>&lt;room id&gt;.png</c>).
/// </summary>
public interface IRoomThumbnailFiles
{
    /// <summary>The room's thumbnail PNG on disk; null when none was drawn.</summary>
    string? GetThumbnailFile(RoomId roomId);
}
