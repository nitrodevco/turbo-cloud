using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>
/// "Generate" on a Web API add-on's key: the room issues a key for the box and the editor is
/// sent it (<c>WiredWebApiKeyResult</c>). Nothing is answered when the room refuses.
/// </summary>
public class WiredGenerateWebApiKeyMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGenerateWebApiKeyMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGenerateWebApiKeyMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.WiredId <= 0)
            return;

        var key = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GenerateWiredWebApiKeyAsync(
                ctx.AsActionContext(),
                message.WiredId,
                message.IsReadKey,
                ct
            )
            .ConfigureAwait(false);

        if (key is null)
            return;

        await ctx.SendComposerAsync(
                new WiredWebApiKeyResultMessageComposer
                {
                    WiredId = message.WiredId,
                    IsReadKey = message.IsReadKey,
                    Key = key,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
