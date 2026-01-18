using System;
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
using Turbo.Primitives.Rooms.Wired.Variable;

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

        var removedIds = new List<WiredVariableId>();
        var checkedIds = new List<WiredVariableId>();
        var diffs = new List<WiredVariableSnapshot>();

        if (message.VariableIdsWithHash.Count > 0)
        {
            foreach (var (id, hash) in message.VariableIdsWithHash)
            {
                checkedIds.Add(id);

                try
                {
                    var existing = variables.Variables.First(x => x.VariableId == id);

                    diffs.Add(existing);
                }
                catch
                {
                    removedIds.Add(id);
                }
            }
        }

        foreach (var variable in variables.Variables)
        {
            if (checkedIds.Contains(variable.VariableId))
                continue;

            diffs.Add(variable);
        }

        _ = ctx.SendComposerAsync(
                new WiredAllVariablesDiffsEventMessageComposer()
                {
                    AllVariablesHash = variables.AllVariablesHash,
                    IsLastChunk = true,
                    RemovedVariableIds = removedIds,
                    AddedOrUpdated = diffs,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
