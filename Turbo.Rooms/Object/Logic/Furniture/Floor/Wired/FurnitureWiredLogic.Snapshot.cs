using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            Logger
        ) ?? [];

    /// <summary>How <paramref name="item"/> stands now, as a snapshot keeps it.</summary>
    protected static WiredFurniSnapshotEntry EntryOf(IRoomFloorItem item) =>
        new(
            item.Logic.GetState(),
            (int)item.Rotation,
            item.X,
            item.Y,
            item.Z.ToInt(),
            item.Definition.Id
        );

    /// <summary>Captures the picked items as they stand now.</summary>
    private void CaptureFurniSnapshot(IEnumerable<int> itemIds)
    {
        var snapshot = new Dictionary<int, WiredFurniSnapshotEntry>();

        foreach (var itemId in itemIds)
        {
            if (!TryGetFloorItem(itemId, out var item))
                continue;

            snapshot[itemId] = EntryOf(item);
        }

        _ctx.RoomObject.ExtraData.UpdateSection(WiredFurniSnapshotEntry.SECTION, snapshot);
    }

    /// <summary>
    /// The editor's "apply furni to set conditions" button: remembers the picked furni as they
    /// stand right now, so a player can arrange them and store that without opening the box and
    /// saving it again. False for a box that keeps no snapshot; the client offers the button
    /// only on the boxes that do.
    /// </summary>
    public bool ApplyFurniSnapshot()
    {
        if (!KeepsFurniSnapshot)
            return false;

        // Writing the section marks the item dirty, so the new snapshot is persisted.
        CaptureFurniSnapshot(GetStuffIds());
        InvalidateSnapshot();

        return true;
    }

    /// <summary>
    /// The snapshot as Habbo writes it into the box's text for the editor: one
    /// "id,state,rotation,x,y,altitude" entry per furni (altitude in hundredths) joined by ";",
    /// with "N" for each part the box leaves out (position: x and y both). Official client
    /// capture 2026-10-24: "2147418140,1,0,6,8,0;2147418135,0,0,8,9,0", state only
    /// "2147418135,0,N,N,N,N". The entries follow the picks as saved (inference: the recorded
    /// picks were sent in that order). Empty without a snapshot, as a fresh box opens.
    /// </summary>
    protected string FormatFurniSnapshot(
        bool state = true,
        bool rotation = true,
        bool position = true,
        bool altitude = true
    )
    {
        static string Part(bool kept, int value) =>
            kept ? value.ToString(CultureInfo.InvariantCulture) : "N";

        return string.Join(
            ';',
            GetFurniSnapshot()
                .Select(x =>
                    string.Join(
                        ',',
                        WiredFurniIds.ToClient(FurniModule, [x.Key])[0],
                        Part(state, x.Value.State),
                        Part(rotation, x.Value.Rotation),
                        Part(position, x.Value.X),
                        Part(position, x.Value.Y),
                        Part(altitude, x.Value.Z)
                    )
                )
        );
    }

    private void DeleteFurniSnapshot() =>
        _ctx.RoomObject.ExtraData.DeleteSection(WiredFurniSnapshotEntry.SECTION);
}
