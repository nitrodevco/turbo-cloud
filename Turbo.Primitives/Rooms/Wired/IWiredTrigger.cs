using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredTrigger
{
    public List<Type> SupportedEventTypes { get; }
    public Task<bool> MatchesAsync(IWiredContext ctx);
    public Task<bool> MatchesEventAsync(RoomEvent evt);
    public Task<bool> CanTriggerAsync(IWiredContext ctx);
}
