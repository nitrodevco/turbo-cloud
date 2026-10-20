using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>
/// "Synchronize" on a variable share add-on: the rooms using the shared variable it stands on
/// take its add-ons again. Nothing is answered.
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

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .SynchronizeSharedWiredAddonsAsync(ctx.AsActionContext(), message.WiredId, ct)
            .ConfigureAwait(false);
    }
}
