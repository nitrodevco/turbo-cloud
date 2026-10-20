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
/// to keep it "after a room reload" (<c>...audience.everyone_info.body</c>). What was shown to
/// selected users only is not kept (inference: those users' clients forget it when they leave).
/// Furni are keyed by the id wired names them with to the client (a wall item negative), users,
/// pets and bots by their room index.
/// </summary>
public sealed partial class RoomWiredSystem
{
    private readonly Dictionary<int, WiredHighlightSnapshot> _furniHighlights = [];
    private readonly Dictionary<int, WiredTransparencySnapshot> _furniTransparencies = [];
    private readonly Dictionary<int, WiredHighlightSnapshot> _userHighlights = [];
    private readonly Dictionary<int, WiredTransparencySnapshot> _userTransparencies = [];

    /// <summary>Keeps a highlight shown to everyone on these furni; one that is off is forgotten.</summary>
    public void KeepFurniHighlight(IEnumerable<int> furniIds, WiredHighlightSnapshot highlight) =>
        Keep(_furniHighlights, furniIds, highlight, highlight.IsOff);

    /// <summary>Keeps a highlight shown to everyone on these users, pets and bots.</summary>
    public void KeepUserHighlight(IEnumerable<int> roomIndices, WiredHighlightSnapshot highlight) =>
        Keep(_userHighlights, roomIndices, highlight, highlight.IsOff);

    /// <summary>Keeps a transparency shown to everyone on these furni; opaque and clickable is forgotten.</summary>
    public void KeepFurniTransparency(
        IEnumerable<int> furniIds,
        WiredTransparencySnapshot transparency
    ) => Keep(_furniTransparencies, furniIds, transparency, transparency.IsOff);

    /// <summary>Keeps a transparency shown to everyone on these users, pets and bots.</summary>
    public void KeepUserTransparency(
        IEnumerable<int> roomIndices,
        WiredTransparencySnapshot transparency
    ) => Keep(_userTransparencies, roomIndices, transparency, transparency.IsOff);

    /// <summary>
    /// The kept states for a player walking in, furni then users; nothing when there are none
    /// (inference: no official capture of an entry yet).
    /// </summary>
    private IEnumerable<IComposer> GetVisualizationEntryState()
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

    private static void Keep<T>(Dictionary<int, T> kept, IEnumerable<int> ids, T value, bool off)
    {
        foreach (var id in ids)
        {
            if (off)
                kept.Remove(id);
            else
                kept[id] = value;
        }
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
