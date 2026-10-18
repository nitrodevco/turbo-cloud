using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// Lists the players with assigned rights for the room settings window. The room grain returns
/// nothing unless the requester is at least the owner.
/// </summary>
public class GetFlatControllersMessageHandler(
    IGrainFactory grainFactory,
    INavigatorService navigatorService
) : IMessageHandler<GetFlatControllersMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        GetFlatControllersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        // Asked before the room grain, whose activation would fail on a missing room.
        if (!await _navigatorService.RoomExistsAsync(message.RoomId, ct).ConfigureAwait(false))
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
