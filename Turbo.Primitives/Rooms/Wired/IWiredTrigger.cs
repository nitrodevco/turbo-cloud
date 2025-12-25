using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredTrigger
{
    public List<Type> SupportedEventTypes { get; }
    public Task<bool> MatchesAsync(IWiredContext ctx);
}
