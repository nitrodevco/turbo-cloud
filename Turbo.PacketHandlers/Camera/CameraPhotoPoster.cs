using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// A photo poster, as Habbo keeps one. Its state is only when it was bought <c>t</c>, in
/// milliseconds, and the photo's id (an official client capture of two rooms: 102 of 102 posters
/// are exactly <c>{"t":&lt;ms&gt;,"id":"&lt;id&gt;"}</c>, <c>t</c> 0.6 to 742 s after the photo's
/// time). The client asks the extra data service (<c>extra_data_service_url</c> + the id,
/// <c>ExternalImageWidget.readFurniJson</c>) for the rest: the photo's extra data, in Habbo's own
/// keys (the same capture, 100 of 100 answers) - <c>ExternalImageWidget.loadPhoto</c> reads its
/// <c>url</c>, <c>creator_name</c>, <c>creator_id</c> and <c>time</c>, the date it shows.
/// </summary>
public static class CameraPhotoPoster
{
    private const string TYPE = "PHOTO";
    private const int VERSION = 1;

    public static string StateOf(CameraPhoto photo, DateTime boughtAt) =>
        JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                ["t"] = new DateTimeOffset(boughtAt).ToUnixTimeMilliseconds(),
                ["id"] = photo.Id,
            }
        );

    public static string ExtraDataOf(CameraPhoto photo, int creatorId, string creatorName) =>
        JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                ["id"] = photo.Id,
                ["url"] = photo.Url,
                ["previewUrl"] = photo.Url,
                ["creator_id"] = creatorId,
                ["creator_name"] = creatorName,
                ["time"] = new DateTimeOffset(photo.CreatedAt).ToUnixTimeMilliseconds(),
                ["room_id"] = photo.RoomId,
                ["type"] = TYPE,
                ["version"] = VERSION,
                ["tags"] = Array.Empty<string>(),
            }
        );
}
