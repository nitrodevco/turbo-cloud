using Orleans;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Navigator.Snapshots;

[GenerateSerializer, Immutable]
public record NavigatorSearchResultSnapshot : RoomInfoSnapshot { }
