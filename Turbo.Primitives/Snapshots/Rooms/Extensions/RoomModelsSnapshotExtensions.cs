using System.Collections.Generic;
using Turbo.Primitives.Snapshots.Rooms.Mapping;

namespace Turbo.Primitives.Snapshots.Rooms.Extensions;

public static class RoomModelsSnapshotExtensions
{
    public static RoomModelSnapshot GetModelById(this RoomModelsSnapshot snapshot, int modelId) =>
        snapshot.ModelsById.TryGetValue(modelId, out var model)
            ? model
            : throw new KeyNotFoundException($"RoomModel:{modelId} not found.");
}
