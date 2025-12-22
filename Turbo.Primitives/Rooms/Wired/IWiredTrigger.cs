using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredTrigger : IWiredDefinition
{
    public List<Type> SupportedEventTypes { get; }
    public Task<bool> MatchesAsync(IWiredContext ctx);
}
