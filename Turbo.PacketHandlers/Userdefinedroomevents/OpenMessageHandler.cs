using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class OpenMessageHandler(IGrainFactory grainFactory) : IMessageHandler<OpenMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        OpenMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.Id <= 0)
            return;

        var wiredData = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetWiredDataSnapshotByFloorItemIdAsync(message.Id, ct)
            .ConfigureAwait(false);

        if (wiredData is null)
            return;

        var wiredType = wiredData.WiredType;
        IComposer? composer = null;

        switch (wiredType)
        {
            case WiredType.Action:
                composer = new WiredFurniActionEventMessageComposer { WiredData = wiredData };
                break;
            case WiredType.Addon:
                composer = new WiredFurniAddonEventMessageComposer { WiredData = wiredData };
                break;
            case WiredType.Condition:
                composer = new WiredFurniConditionEventMessageComposer { WiredData = wiredData };
                break;
            case WiredType.Selector:
                composer = new WiredFurniSelectorEventMessageComposer { WiredData = wiredData };
                break;
            case WiredType.Trigger:
                composer = new WiredFurniTriggerEventMessageComposer { WiredData = wiredData };
                break;
            case WiredType.Variable:
                composer = new WiredFurniVariableEventMessageComposer { WiredData = wiredData };
                break;
        }

        if (composer is null)
            return;

        _ = ctx.SendComposerAsync(composer, ct).ConfigureAwait(false);
    }
}
