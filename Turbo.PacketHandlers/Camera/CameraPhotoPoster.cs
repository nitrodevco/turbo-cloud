using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// A photo poster, as Habbo keeps one. Its state is only when the photo was taken <c>t</c>, in
/// milliseconds, and the photo's id (an official client capture of two rooms: 102 of 102 posters
/// are exactly <c>{"t":&lt;ms&gt;,"id":"&lt;id&gt;"}</c>). The client asks the extra data service
/// (<c>extra_data_service_url</c> + the id, <c>ExternalImageWidget.readFurniJson</c>) for the rest:
/// the photo's extra data, what <c>ExternalImageWidget.loadPhoto</c> reads - its url <c>w</c>, the
/// photographer's name <c>n</c> and id <c>s</c>, the photo's id <c>u</c> and its time <c>t</c>.
/// </summary>
public static class CameraPhotoPoster
{
    public static string StateOf(CameraPhoto photo) =>
        JsonSerializer.Serialize(
            new Dictionary<string, object> { ["t"] = TimeOf(photo), ["id"] = photo.Id }
        );

    public static string ExtraDataOf(CameraPhoto photo, int creatorId, string creatorName) =>
        JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                ["t"] = TimeOf(photo),
                ["u"] = photo.Id,
                ["s"] = creatorId,
                ["n"] = creatorName,
                ["w"] = photo.Url,
            }
        );

    private static long TimeOf(CameraPhoto photo) =>
        new DateTimeOffset(photo.CreatedAt).ToUnixTimeMilliseconds();
}
