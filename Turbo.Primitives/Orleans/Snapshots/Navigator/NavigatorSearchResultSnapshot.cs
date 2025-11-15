using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Primitives.Orleans.Snapshots.Navigator;

[GenerateSerializer, Immutable]
public record NavigatorSearchResultSnapshot : RoomInfoSnapshot { }
