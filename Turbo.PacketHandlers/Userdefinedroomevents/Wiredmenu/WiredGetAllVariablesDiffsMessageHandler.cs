using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

public class WiredGetAllVariablesDiffsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGetAllVariablesDiffsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGetAllVariablesDiffsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var variables = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetWiredVariablesSnapshotAsync(ct)
            .ConfigureAwait(false);

        var removedVariables = new List<long>();
        var checkedVariables = new List<long>();
        var variableDiffs = new List<WiredVariableSnapshot>();

        if (message.VariableHashes.Count > 0)
        {
            foreach (var (id, hash) in message.VariableHashes)
            {
                checkedVariables.Add(id);

                try
                {
                    var existing = variables.Variables.First(x => x.VariableId.ToInt64() == id);
                    variableDiffs.Add(existing);
                }
                catch
                {
                    removedVariables.Add(id);
                }
            }
        }

        foreach (var variable in variables.Variables)
        {
            if (checkedVariables.Contains((long)variable.VariableId))
                continue;

            variableDiffs.Add(variable);
        }

        _ = ctx.SendComposerAsync(
                new WiredAllVariablesDiffsEventMessageComposer()
                {
                    AllVariablesHash = variables.AllVariablesHash,
                    IsLastChunk = true,
                    RemovedVariables = removedVariables,
                    AddedOrUpdated = variableDiffs,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
