using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Triggers;

public abstract class WiredTrigger : WiredDefinition, IWiredTrigger
{
    public abstract List<Type> SupportedEventTypes { get; }
    public abstract Task<bool> MatchesAsync(IWiredContext ctx);
}
