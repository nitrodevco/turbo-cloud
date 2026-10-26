using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Navigator;

/// <summary>A room in a Public tab category, as staff see it.</summary>
public sealed record PublicRoomEntry(
    RoomId RoomId,
    string Name,
    PlayerId OwnerId,
    string OwnerName,
    DateTime AddedAt,
    PlayerId? AddedBy
);

/// <summary>A Public tab category with its rooms in their order, hidden ones too.</summary>
public sealed record PublicCategoryEntry(
    int Id,
    string Name,
    int OrderNum,
    bool ForceClosed,
    NavigatorViewModeType ViewMode,
    bool Visible,
    NavigatorPublicCategoryKind Kind,
    string? PluginKey,
    string SearchCode,
    ImmutableArray<PublicRoomEntry> Rooms
);

/// <summary>A category's fields to set; null leaves one as it is (a new category: name required, shown, open, thumbnails).</summary>
public sealed record PublicCategoryChange(
    string? Name = null,
    bool? Visible = null,
    bool? ForceClosed = null,
    NavigatorViewModeType? ViewMode = null
);

/// <summary>
/// The navigator's Public tab as staff and plugins edit it: its categories (Habbo's
/// <c>official_&lt;id&gt;</c> blocks) and the rooms in them. Every change is seen by the next
/// search on every silo (listing key <see cref="NavigatorListingKeys.PUBLIC"/>); players get no
/// push, as on Habbo. Refusals throw <see cref="ArgumentException"/> with why.
/// </summary>
public interface IPublicRoomEditor
{
    public Task<ImmutableArray<PublicCategoryEntry>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Adds a category (<paramref name="id"/> 0, last in the tab, of <paramref name="kind"/>) or
    /// changes one (its kind stays). A plugin's category carries its <paramref name="pluginKey"/>,
    /// one per key; staff's have none.
    /// </summary>
    public Task<int> SaveCategoryAsync(
        int id,
        PublicCategoryChange change,
        NavigatorPublicCategoryKind kind,
        string? pluginKey,
        CancellationToken ct
    );

    /// <summary>Removes a category and its room list; false when there is none.</summary>
    public Task<bool> DeleteCategoryAsync(int id, CancellationToken ct);

    /// <summary>Puts the categories in this order: every category, each once.</summary>
    public Task OrderCategoriesAsync(IReadOnlyList<int> categoryIds, CancellationToken ct);

    /// <summary>Puts the room in the category, first or last; false when it is in it already.</summary>
    public Task<bool> AddRoomAsync(
        int categoryId,
        RoomId roomId,
        bool first,
        PlayerId? by,
        CancellationToken ct
    );

    /// <summary>Moves the room from one category to another, at <paramref name="index"/> (null: first).</summary>
    public Task MoveRoomAsync(
        int fromCategoryId,
        RoomId roomId,
        int toCategoryId,
        int? index,
        CancellationToken ct
    );

    /// <summary>Puts the category's rooms in this order: every room in it, each once.</summary>
    public Task OrderRoomsAsync(
        int categoryId,
        IReadOnlyList<RoomId> roomIds,
        CancellationToken ct
    );

    /// <summary>Takes the room out of the category; false when it was not in it.</summary>
    public Task<bool> RemoveRoomAsync(int categoryId, RoomId roomId, CancellationToken ct);
}
