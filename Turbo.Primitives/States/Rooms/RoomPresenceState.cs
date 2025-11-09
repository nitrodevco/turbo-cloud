using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.States.Rooms;

[GenerateSerializer]
public sealed class RoomPresenceState
{
    [Id(0)]
    public HashSet<long> PlayerIds { get; set; } = [];
}
