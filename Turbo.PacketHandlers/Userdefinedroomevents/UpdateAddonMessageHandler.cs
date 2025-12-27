using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateAddonMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UpdateAddonMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UpdateAddonMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.Id <= 0)
            return;

        if (
            !await _grainFactory
                .GetRoomGrain(ctx.RoomId)
                .ApplyWiredUpdateAsync(ctx.AsActionContext(), message.Id, message, ct)
                .ConfigureAwait(false)
        )
            return;

        _ = ctx.SendComposerAsync(new WiredSaveSuccessEventMessageComposer(), ct)
            .ConfigureAwait(false);
    }
}
