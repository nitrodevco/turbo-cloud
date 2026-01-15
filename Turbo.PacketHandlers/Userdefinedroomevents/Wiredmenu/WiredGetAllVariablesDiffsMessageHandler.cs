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
using Turbo.Primitives.Rooms.Snapshots.Wired;

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

        var removedVariables = new List<int>();
        var checkedVariables = new List<int>();
        var variableDiffs = new List<WiredVariableSnapshot>();

        if (message.VariableHashes.Count > 0)
        {
            foreach (var (id, hash) in message.VariableHashes)
            {
                try
                {
                    var existing = variables.Variables.First(x => x.HashCode == hash);

                    variableDiffs.Add(existing);
                }
                catch
                {
                    removedVariables.Add(hash);
                }
            }
        }

        foreach (var variable in variables.Variables)
        {
            if (checkedVariables.Contains(variable.HashCode))
                continue;

            variableDiffs.Add(variable);
        }

        _ = ctx.SendComposerAsync(
                new WiredAllVariablesDiffsEventMessageComposer()
                {
                    AllVariablesHash = variables.GlobalHash,
                    IsLastChunk = true,
                    RemovedVariables = removedVariables,
                    AddedOrUpdated = variableDiffs,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
