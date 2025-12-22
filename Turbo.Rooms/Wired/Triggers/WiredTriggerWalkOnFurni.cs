using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Triggers;

[WiredDefinition("wf_trg_walks_on_furni")]
public class WiredTriggerWalkOnFurni : WiredTrigger
{
    public override List<Type> SupportedEventTypes { get; } = [typeof(AvatarWalkOnFurniEvent)];

    public override Task<bool> MatchesAsync(IWiredContext ctx)
    {
        var result = ctx.Event is AvatarWalkOnFurniEvent;

        return Task.FromResult(result);
    }
}
