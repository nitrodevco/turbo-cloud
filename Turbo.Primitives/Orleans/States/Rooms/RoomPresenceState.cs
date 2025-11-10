using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Orleans.States.Rooms;

[GenerateSerializer]
public sealed class RoomPresenceState
{
    [Id(0)]
    public HashSet<long> PlayerIds { get; set; } = [];

    [Id(1)]
    public HashSet<string> SessionIds { get; set; } = [];
}
