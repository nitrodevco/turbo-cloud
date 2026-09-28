using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Navigator;

[RequiresPermission(PermissionNodes.Navigator.STAFF_PICK)]
public class ToggleStaffPickMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ToggleStaffPickMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ToggleStaffPickMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (message.RoomId <= 0)
            return;

        // The client sends the room's current state; the request is to flip it.
        await _grainFactory
            .GetRoomGrain(message.RoomId)
            .SetStaffPickAsync(!message.IsStaffPicked, ct)
            .ConfigureAwait(false);
    }
}
