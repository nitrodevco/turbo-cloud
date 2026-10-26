using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Navigator.Snapshots;

/// <summary>
/// A category of the navigator's Public tab (<c>official_view</c>): one result block, titled by
/// its name, with the rooms staff put in it in their order. A room can be in several.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record NavigatorPublicCategorySnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    /// <summary>The block's title as sent: a literal name or a <c>${text.key}</c>; empty lets the client title it from its code.</summary>
    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required int OrderNum { get; init; }

    /// <summary>The block arrives collapsed, every time it is drawn.</summary>
    [Id(3)]
    public required bool ForceClosed { get; init; }

    [Id(4)]
    public required NavigatorViewModeType ViewMode { get; init; }

    /// <summary>Hidden categories are kept for staff and not sent to players.</summary>
    [Id(5)]
    public required bool Visible { get; init; }

    [Id(6)]
    public required NavigatorPublicCategoryKind Kind { get; init; }

    /// <summary>The plugin that made it, by its own key; null when staff did.</summary>
    [Id(7)]
    public string? PluginKey { get; init; }

    /// <summary>Its rooms in their order; empty for <see cref="NavigatorPublicCategoryKind.StaffPicks"/>, whose rooms are the flagged ones.</summary>
    [Id(8)]
    public required ImmutableArray<RoomId> RoomIds { get; init; }

    /// <summary>The block's search code: <c>staffpicks</c>, or <c>official_&lt;id&gt;</c> as Habbo's.</summary>
    public string SearchCode =>
        Kind == NavigatorPublicCategoryKind.StaffPicks
            ? NavigatorSearchCodes.STAFF_PICKS
            : NavigatorSearchCodes.Official(Id);
}
