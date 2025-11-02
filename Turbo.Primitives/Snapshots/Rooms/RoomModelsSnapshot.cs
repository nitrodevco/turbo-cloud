using System.Collections.Immutable;
using Turbo.Contracts.Enums.Rooms.Object;

namespace Turbo.Primitives.Snapshots.Rooms;

public sealed record RoomModelsSnapshot(ImmutableDictionary<int, RoomModelSnapshot> ModelsById);
