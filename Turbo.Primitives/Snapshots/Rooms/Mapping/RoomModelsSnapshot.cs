using System.Collections.Immutable;

namespace Turbo.Primitives.Snapshots.Rooms.Mapping;

public sealed record RoomModelsSnapshot(ImmutableDictionary<int, RoomModelSnapshot> ModelsById);
