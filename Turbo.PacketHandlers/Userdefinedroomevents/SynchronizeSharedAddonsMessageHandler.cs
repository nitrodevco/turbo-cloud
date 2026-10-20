using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>
/// "Synchronize" on a variable share add-on: the rooms using the shared variable it stands on
/// take its add-ons again. Habbo answers with WiredSaveSuccess (official client capture lab60,
/// WIN63-202610201032: 163 ms after the button); nothing is answered when the room refuses.
/// </summary>
public class SynchronizeSharedAddonsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SynchronizeSharedAddonsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SynchronizeSharedAddonsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.WiredId <= 0)
            return;

        if (
            !await _grainFactory
                .GetRoomGrain(ctx.RoomId)
                .SynchronizeSharedWiredAddonsAsync(ctx.AsActionContext(), message.WiredId, ct)
                .ConfigureAwait(false)
        )
            return;

        await ctx.SendComposerAsync(new WiredSaveSuccessEventMessageComposer(), ct)
            .ConfigureAwait(false);
    }
}
