using System.Diagnostics.CodeAnalysis;
using Orleans;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Navigator.Snapshots;

/// <summary>
/// A room as a navigator search returns it. It adds nothing to the room info — the type exists
/// so a search result is not mistaken for the live room — which is why the only constructor that
/// matters is the one that takes the info whole.
/// </summary>
[GenerateSerializer, Immutable]
public record NavigatorSearchResultSnapshot : RoomInfoSnapshot
{
    public NavigatorSearchResultSnapshot() { }

    /// <summary>
    /// Copies the room info in one go. It used to be built field by field in
    /// <c>NavigatorService.ToSearchResult</c>, and had silently stopped copying two of them:
    /// <c>HiddenByBc</c>, so a room hidden by Builders Club was still listed, and later
    /// <c>Guild</c>, so a group's badge never reached a search result. Neither field is
    /// required, so neither failed to compile.
    /// </summary>
    [SetsRequiredMembers]
    public NavigatorSearchResultSnapshot(RoomInfoSnapshot info)
        : base(info) { }
}
