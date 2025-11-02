using System.Collections.Generic;

namespace Turbo.Primitives.Snapshots.Rooms.Extensions;

public static class RoomModelsSnapshotExtensions
{
    public static RoomModelSnapshot GetModelById(this RoomModelsSnapshot snapshot, int modelId) =>
        snapshot.ModelsById.TryGetValue(modelId, out var model)
            ? model
            : throw new KeyNotFoundException($"RoomModel:{modelId} not found.");
}
