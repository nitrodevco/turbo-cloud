using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

internal static class WiredVariableHoldersExtensions
{
    /// <summary>
    /// Sends the wired menu who holds a variable in the room. Asked for by the overview tab, and
    /// sent again after it wiped a variable so the list it shows empties.
    /// </summary>
    public static async Task SendWiredVariableHoldersAsync(
        this MessageContext ctx,
        IGrainFactory grainFactory,
        WiredVariableId variableId,
        CancellationToken ct
    )
    {
        var holders = await grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetWiredVariableHoldersAsync(ctx.AsActionContext(), variableId, ct)
            .ConfigureAwait(false);

        if (holders is null)
            return;

        await ctx.SendComposerAsync(
                new WiredAllVariableHoldersEventMessageComposer
                {
                    VariableSnapshot = holders.Variable,
                    ObjectValues = holders
                        .Holders.Select(x => (RoomObjectId.Parse(x.objectId), x.value))
                        .ToList(),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
