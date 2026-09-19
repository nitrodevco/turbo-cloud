using System;
using System.Collections.Generic;
using System.Text.Json;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

/// <summary>
/// The furni snapshot the "match snapshot" condition and "match to snapshot" action keep: the
/// state, rotation, position and altitude of every picked item at the moment the box was saved.
/// Stored beside the wired data in the item extra data.
/// </summary>
public abstract partial class FurnitureWiredLogic
{
    /// <summary>
    /// Set by the boxes that compare against a saved snapshot. The base then captures the picked
    /// furni on every save and drops the snapshot when the box is picked up, so an action and a
    /// condition (which share no other base) do not each repeat that lifecycle.
    /// </summary>
    protected virtual bool KeepsFurniSnapshot => false;

    protected Dictionary<int, WiredFurniSnapshotEntry> GetFurniSnapshot() =>
        FurnitureExtraDataSections.Read<Dictionary<int, WiredFurniSnapshotEntry>>(
            _ctx.RoomObject.ExtraData,
            WiredFurniSnapshotEntry.SECTION,
            _roomGrain._logger
        ) ?? [];

    /// <summary>Captures the picked items as they stand now.</summary>
    private void CaptureFurniSnapshot(IEnumerable<int> itemIds)
    {
        var snapshot = new Dictionary<int, WiredFurniSnapshotEntry>();

        foreach (var itemId in itemIds)
        {
            if (!TryGetFloorItem(itemId, out var item))
                continue;

            snapshot[itemId] = new WiredFurniSnapshotEntry(
                item.Logic.GetState(),
                (int)item.Rotation,
                item.X,
                item.Y,
                item.Z.ToInt()
            );
        }

        _ctx.RoomObject.ExtraData.UpdateSection(WiredFurniSnapshotEntry.SECTION, snapshot);
    }

    private void DeleteFurniSnapshot() =>
        _ctx.RoomObject.ExtraData.DeleteSection(WiredFurniSnapshotEntry.SECTION);
}
