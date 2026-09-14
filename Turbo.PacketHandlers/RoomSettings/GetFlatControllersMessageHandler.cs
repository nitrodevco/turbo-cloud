using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// Lists the players with assigned rights for the room settings window. The room grain returns
/// nothing unless the requester is at least the owner.
/// </summary>
public class GetFlatControllersMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetFlatControllersMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetFlatControllersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        var controllers = await _grainFactory
            .GetRoomGrain(message.RoomId)
            .GetControllersAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);

        if (controllers is null)
            return;

        await ctx.SendComposerAsync(
                new FlatControllersEventMessageComposer
                {
                    RoomId = message.RoomId,
                    Controllers = controllers.Value,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
