using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Messages.Outgoing.Camera;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// The photo lab's Preview: the render data is kept as the player's last photo, drawn into its PNG
/// (<see cref="CameraRenderer"/>) and its url sent back (<c>CameraStorageUrlMessage</c>); an empty
/// url past the day's limit, or for data that is not a render, is the client's
/// <c>camera.render.count.info</c>. The render counts before it is inflated, so data that is not
/// a render is refused within the day's limit too.
/// </summary>
public class RenderRoomMessageHandler(
    CameraPhotoStore store,
    CameraRenderer renderer,
    IOptions<CameraConfig> config
) : IMessageHandler<RenderRoomMessage>
{
    private readonly CameraConfig _config = config.Value;

    public async ValueTask HandleAsync(
        RenderRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var url = string.Empty;

        if (
            store.TryCount(ctx.PlayerId.Value, "photo", _config.RenderLimitPerDay)
            && store.Inflate(message.Data) is { } json
        )
        {
            var photo = await store
                .SavePhotoAsync(ctx.PlayerId.Value, json, ct)
                .ConfigureAwait(false);

            // With its small copy: bought, the photo is a poster, and a poster draws that one.
            await renderer
                .RenderAsync(photo.JsonPath, photo.PngPath, ct, withSmall: true)
                .ConfigureAwait(false);
            url = photo.Url;
        }

        await ctx.SendComposerAsync(new CameraStorageUrlMessageComposer { Url = url }, ct)
            .ConfigureAwait(false);
    }
}
