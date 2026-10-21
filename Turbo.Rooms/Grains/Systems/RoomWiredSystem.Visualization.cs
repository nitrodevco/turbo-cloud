using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// What WIRED Set Highlight and Set Visibility showed to "everyone, including users who enter
/// later" (<c>wiredfurni.params.furni_visualization.audience.everyone</c>): kept for the room's
/// life and told to whoever walks in (Flash <c>WiredFurniVisualizationState</c> /
/// <c>WiredUserVisualizationState</c>, WIN63-202610201032). A room that unloads forgets it, which
/// is why Habbo tells the builder to "stack this WIRED Effect with WIRED Trigger: Room Initialized"
/// to keep it "after a room reload" (<c>...audience.everyone_info.body</c>). Official client capture
/// (lab60 2026-10-20): a later fire replaces a furni's entry, strength 0 stays as an entry, picking
/// up the target drops it, selected-users fires are not kept, an empty state is not sent.
/// Furni are keyed by the id wired names them with to the client (a wall item negative), users,
/// pets and bots by their room index.
/// </summary>
public sealed partial class RoomWiredSystem
{
    private readonly Dictionary<int, WiredHighlightSnapshot> _furniHighlights = [];
    private readonly Dictionary<int, WiredTransparencySnapshot> _furniTransparencies = [];
    private readonly Dictionary<int, WiredHighlightSnapshot> _userHighlights = [];
    private readonly Dictionary<int, WiredTransparencySnapshot> _userTransparencies = [];

    /// <summary>Keeps a highlight shown to everyone on these furni, replacing the one before (strength 0 included).</summary>
    public void KeepFurniHighlight(IEnumerable<int> furniIds, WiredHighlightSnapshot highlight) =>
        Keep(_furniHighlights, furniIds, highlight);

    /// <summary>Keeps a highlight shown to everyone on these users, pets and bots.</summary>
    public void KeepUserHighlight(IEnumerable<int> roomIndices, WiredHighlightSnapshot highlight) =>
        Keep(_userHighlights, roomIndices, highlight);

    /// <summary>Keeps a transparency shown to everyone on these furni (a restore to 0 included).</summary>
    public void KeepFurniTransparency(
        IEnumerable<int> furniIds,
        WiredTransparencySnapshot transparency
    ) => Keep(_furniTransparencies, furniIds, transparency);

    /// <summary>Keeps a transparency shown to everyone on these users, pets and bots.</summary>
    public void KeepUserTransparency(
        IEnumerable<int> roomIndices,
        WiredTransparencySnapshot transparency
    ) => Keep(_userTransparencies, roomIndices, transparency);

    /// <summary>
    /// The kept states for a player walking in, furni then users, which the entry sends right
    /// after the first Users and before Objects (<c>RoomService</c>); nothing when there are none
    /// (official client capture, 3 of 3 entries each way).
    /// </summary>
    public IEnumerable<IComposer> GetVisualizationEntryState()
    {
        if (_furniTransparencies.Count > 0 || _furniHighlights.Count > 0)
            yield return new WiredFurniVisualizationStateMessageComposer
            {
                Transparencies = TransparencyEntries(_furniTransparencies),
                Highlights = HighlightEntries(_furniHighlights),
            };

        if (_userTransparencies.Count > 0 || _userHighlights.Count > 0)
            yield return new WiredUserVisualizationStateMessageComposer
            {
                Transparencies = TransparencyEntries(_userTransparencies),
                Highlights = HighlightEntries(_userHighlights),
            };
    }

    /// <summary>A furni that left the room takes its states along, whether it was on the floor or the wall.</summary>
    private void ForgetFurniVisualization(int objectId)
    {
        foreach (var id in new[] { objectId, -objectId })
        {
            _furniHighlights.Remove(id);
            _furniTransparencies.Remove(id);
        }
    }

    private void ForgetUserVisualization(int roomIndex)
    {
        _userHighlights.Remove(roomIndex);
        _userTransparencies.Remove(roomIndex);
    }

    private static void Keep<T>(Dictionary<int, T> kept, IEnumerable<int> ids, T value)
    {
        foreach (var id in ids)
            kept[id] = value;
    }

    private static ImmutableArray<WiredHighlightStateEntry> HighlightEntries(
        Dictionary<int, WiredHighlightSnapshot> kept
    ) =>
        [
            .. kept.OrderBy(x => x.Key)
                .Select(x => new WiredHighlightStateEntry { Id = x.Key, Highlight = x.Value }),
        ];

    private static ImmutableArray<WiredTransparencyStateEntry> TransparencyEntries(
        Dictionary<int, WiredTransparencySnapshot> kept
    ) =>
        [
            .. kept.OrderBy(x => x.Key)
                .Select(x => new WiredTransparencyStateEntry
                {
                    Id = x.Key,
                    Transparency = x.Value,
                }),
        ];
}
