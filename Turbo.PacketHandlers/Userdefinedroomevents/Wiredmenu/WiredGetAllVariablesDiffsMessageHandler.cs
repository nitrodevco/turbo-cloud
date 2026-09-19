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
            .GetWiredVariablesSnapshotAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);

        if (variables is null)
            return;

        // The client lists what it already holds. It gets back the ids that are gone and the
        // variables that are new to it or whose hash moved; what it has unchanged is not resent.
        var current = variables.Variables.ToDictionary(x => x.VariableId);
        var known = new HashSet<WiredVariableId>();
        var removedIds = new List<WiredVariableId>();
        var diffs = new List<WiredVariableSnapshot>();

        foreach (var (id, hash) in message.VariableIdsWithHash)
        {
            if (!known.Add(id))
                continue;

            if (!current.TryGetValue(id, out var existing))
                removedIds.Add(id);
            else if (existing.VariableHash != hash)
                diffs.Add(existing);
        }

        diffs.AddRange(variables.Variables.Where(x => !known.Contains(x.VariableId)));

        await ctx.SendComposerAsync(
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
