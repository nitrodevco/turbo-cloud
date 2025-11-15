using System;
using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Orleans.States.Room;

[GenerateSerializer]
public sealed class RoomPresenceState
{
    [Id(0)]
    public required HashSet<long> PlayerIds { get; set; } = [];

    [Id(1)]
    public required DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
