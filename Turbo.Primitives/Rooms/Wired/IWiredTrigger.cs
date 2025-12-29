using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredTrigger : IWiredItem
{
    public List<Type> SupportedEventTypes { get; }
    public Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct);
    public Task<bool> CanTriggerAsync(IWiredContext ctx, CancellationToken ct);
}
