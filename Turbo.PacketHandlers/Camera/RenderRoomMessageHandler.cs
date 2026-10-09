using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Messages.Outgoing.Camera;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// The photo lab's Preview: the render data is kept as the player's last photo and its url sent
/// back (<c>CameraStorageUrlMessage</c>); an empty url past the day's limit, or for data that is
/// not a render, is the client's <c>camera.render.count.info</c>.
/// </summary>
public class RenderRoomMessageHandler(CameraPhotoStore store, IOptions<CameraConfig> config)
    : IMessageHandler<RenderRoomMessage>
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

        var json = CameraPhotoStore.Inflate(message.Data);
        var url = string.Empty;

        if (
            json is not null
            && store.TryCount(ctx.PlayerId.Value, "photo", _config.RenderLimitPerDay)
        )
            url = (
                await store.SavePhotoAsync(ctx.PlayerId.Value, json, ct).ConfigureAwait(false)
            ).Url;

        await ctx.SendComposerAsync(new CameraStorageUrlMessageComposer { Url = url }, ct)
            .ConfigureAwait(false);
    }
}
