using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Navigator;

public class ToggleStaffPickMessageHandler(
    IGrainFactory grainFactory,
    INavigatorService navigatorService
) : IMessageHandler<ToggleStaffPickMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        ToggleStaffPickMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || message.RoomId <= 0
            || !_navigatorService.CanManageStaffPicks(ctx.PlayerId)
        )
            return;

        // The client sends the room's current state; the request is to flip it.
        await _grainFactory
            .GetRoomGrain(message.RoomId)
            .SetStaffPickAsync(!message.IsStaffPicked, ct)
            .ConfigureAwait(false);
    }
}
