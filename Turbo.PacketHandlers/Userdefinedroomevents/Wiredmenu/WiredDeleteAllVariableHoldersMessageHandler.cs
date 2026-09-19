using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// The wired menu's overview tab deleted a variable for everyone: the room takes it from every
/// holder, and the menu is sent the holders again so its list empties.
/// </summary>
public class WiredDeleteAllVariableHoldersMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredDeleteAllVariableHoldersMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredDeleteAllVariableHoldersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || !WiredVariableId.TryParse(message.SelectedVariableId, out var variableId)
        )
            return;

        if (
            !await _grainFactory
                .GetRoomGrain(ctx.RoomId)
                .RemoveWiredVariableFromAllHoldersAsync(ctx.AsActionContext(), variableId, ct)
                .ConfigureAwait(false)
        )
            return;

        await ctx.SendWiredVariableHoldersAsync(_grainFactory, variableId, ct)
            .ConfigureAwait(false);
    }
}
