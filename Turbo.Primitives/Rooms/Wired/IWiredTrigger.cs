using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredTrigger : IWiredBox
{
    public List<Type> SupportedEventTypes { get; }
    public Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct);
    public Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct);
}
