using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

/// <summary>The wired menu asked which furni or users hold a value for one variable.</summary>
public class WiredGetAllVariableHoldersMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGetAllVariableHoldersMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGetAllVariableHoldersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        if (
            !ulong.TryParse(
                message.SelectedVariableId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var rawId
            )
        )
            return;

        var holders = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetWiredVariableHoldersAsync(ctx.AsActionContext(), new WiredVariableId(rawId), ct)
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
