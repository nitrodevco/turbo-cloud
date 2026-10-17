using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// A photo poster's state: the photo as the JSON Habbo's external image furni read
/// (<c>ExternalImageWidget.loadPhoto</c>): its url <c>w</c>, the photographer's name <c>n</c> and
/// id <c>s</c>, the photo's id <c>u</c> and when it was taken <c>t</c>, in milliseconds (the
/// client shows <c>new Date(t)</c> under the picture, with the name).
/// </summary>
public static class CameraPhotoPoster
{
    public static string StateOf(CameraPhoto photo, int creatorId, string creatorName) =>
        JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                ["t"] = new DateTimeOffset(photo.CreatedAt).ToUnixTimeMilliseconds(),
                ["u"] = photo.Id,
                ["s"] = creatorId,
                ["n"] = creatorName,
                ["w"] = photo.Url,
            }
        );
}
