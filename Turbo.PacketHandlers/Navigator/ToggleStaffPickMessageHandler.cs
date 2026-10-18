using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Navigator;

[RequiresPermission(PermissionNodes.Navigator.STAFF_PICK)]
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
        if (message.RoomId <= 0)
            return;

        // Asked before the room grain, whose activation would fail on a missing room.
        if (!await _navigatorService.RoomExistsAsync(message.RoomId, ct).ConfigureAwait(false))
            return;

        // The client sends the room's current state; the request is to flip it.
        await _grainFactory
            .GetRoomGrain(message.RoomId)
            .SetStaffPickAsync(!message.IsStaffPicked, ct)
            .ConfigureAwait(false);
    }
}
