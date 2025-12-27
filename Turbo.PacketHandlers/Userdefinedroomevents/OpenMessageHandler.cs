using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;

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

        IComposer? composer = null;

        switch (wiredData)
        {
            case WiredDataActionSnapshot actionSnapshot:
                composer = new WiredFurniActionEventMessageComposer { WiredData = actionSnapshot };
                break;
            case WiredDataAddonSnapshot addonSnapshot:
                composer = new WiredFurniAddonEventMessageComposer { WiredData = addonSnapshot };
                break;
            case WiredDataConditionSnapshot conditionSnapshot:
                composer = new WiredFurniConditionEventMessageComposer
                {
                    WiredData = conditionSnapshot,
                };
                break;
            case WiredDataSelectorSnapshot selectorSnapshot:
                composer = new WiredFurniSelectorEventMessageComposer
                {
                    WiredData = selectorSnapshot,
                };
                break;
            case WiredDataTriggerSnapshot triggerSnapshot:
                composer = new WiredFurniTriggerEventMessageComposer
                {
                    WiredData = triggerSnapshot,
                };
                break;
            case WiredDataVariableSnapshot variableSnapshot:
                composer = new WiredFurniVariableEventMessageComposer
                {
                    WiredData = variableSnapshot,
                };
                break;
        }

        if (composer is null)
            return;

        _ = ctx.SendComposerAsync(composer, ct).ConfigureAwait(false);
    }
}
