using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Admin.Commands;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Main.Hotel;

/// <summary>The hotel API's navigator over the host's own Public tab editor.</summary>
internal sealed class HostHotelNavigator(HostHotelPublicRooms publicRooms) : IHotelNavigator
{
    public IHotelPublicRooms Public { get; } = publicRooms;
}

/// <summary>
/// <see cref="IHotelPublicRooms"/> over <see cref="IPublicRoomEditor"/>, the editor the admin
/// panel's Public rooms page uses, each change in the command log as the plugin's.
/// </summary>
internal sealed class HostHotelPublicRooms(IPublicRoomEditor editor, AdminActionLog actions)
    : IHotelPublicRooms
{
    public async Task<IReadOnlyList<HotelPublicCategory>> CategoriesAsync(CancellationToken ct) =>
        [.. (await editor.ListAsync(ct).ConfigureAwait(false)).Select(ToHotel)];

    public async Task<HotelPublicCategory> EnsureCategoryAsync(
        string key,
        HotelPublicCategoryDefaults defaults,
        CancellationToken ct
    )
    {
        HotelPublicKey.ThrowIfInvalid(key);
        ArgumentNullException.ThrowIfNull(defaults);

        if (await FindAsync(key, ct).ConfigureAwait(false) is { } existing)
            return ToHotel(existing);

        var id = await editor
            .SaveCategoryAsync(
                0,
                new PublicCategoryChange(
                    defaults.Name,
                    defaults.Visible,
                    defaults.ForceClosed,
                    defaults.Thumbnails ? NavigatorViewModeType.Tiles : NavigatorViewModeType.Rows
                ),
                NavigatorPublicCategoryKind.Manual,
                key,
                ct
            )
            .ConfigureAwait(false);

        foreach (var room in defaults.Rooms ?? [])
            await editor.AddRoomAsync(id, room, false, null, ct).ConfigureAwait(false);

        await LogAsync(key, $"category add {id} {defaults.Name}").ConfigureAwait(false);

        return ToHotel(await FindAsync(key, ct).ConfigureAwait(false) ?? throw Missing(key));
    }

    public async Task<bool> AddRoomAsync(string key, RoomId room, bool first, CancellationToken ct)
    {
        var category = await OwnAsync(key, ct).ConfigureAwait(false);
        var added = await editor
            .AddRoomAsync(category.Id, room, first, null, ct)
            .ConfigureAwait(false);

        if (added)
            await LogAsync(key, $"room add {category.Id} {room.Value} {(first ? "first" : "last")}")
                .ConfigureAwait(false);

        return added;
    }

    public async Task<bool> RemoveRoomAsync(string key, RoomId room, CancellationToken ct)
    {
        var category = await OwnAsync(key, ct).ConfigureAwait(false);
        var removed = await editor.RemoveRoomAsync(category.Id, room, ct).ConfigureAwait(false);

        if (removed)
            await LogAsync(key, $"room remove {category.Id} {room.Value}").ConfigureAwait(false);

        return removed;
    }

    public async Task OrderRoomsAsync(string key, IReadOnlyList<RoomId> rooms, CancellationToken ct)
    {
        var category = await OwnAsync(key, ct).ConfigureAwait(false);

        await editor.OrderRoomsAsync(category.Id, rooms, ct).ConfigureAwait(false);
        await LogAsync(
                key,
                $"room order {category.Id} {string.Join(',', rooms.Select(x => x.Value))}"
            )
            .ConfigureAwait(false);
    }

    private async Task<PublicCategoryEntry?> FindAsync(string key, CancellationToken ct) =>
        (await editor.ListAsync(ct).ConfigureAwait(false)).FirstOrDefault(x =>
            string.Equals(x.PluginKey, key, StringComparison.Ordinal)
        );

    private async Task<PublicCategoryEntry> OwnAsync(string key, CancellationToken ct)
    {
        HotelPublicKey.ThrowIfInvalid(key);

        return await FindAsync(key, ct).ConfigureAwait(false) ?? throw Missing(key);
    }

    private static ArgumentException Missing(string key) =>
        new($"There is no public category {key}: make it with EnsureCategoryAsync.", nameof(key));

    /// <summary>The panel's line for the change, the plugin's key after it, as no player from the plugin.</summary>
    private Task LogAsync(string key, string arguments) =>
        actions.WriteAsync(
            AdminActionLog.NAVIGATOR,
            new PlayerId(0),
            $"public {arguments} {key}",
            CommandOutcome.Completed,
            AdminActionLog.PLUGIN_SOURCE,
            CancellationToken.None
        );

    private static HotelPublicCategory ToHotel(PublicCategoryEntry x) =>
        new(
            x.Id,
            x.PluginKey,
            x.Name,
            x.Visible,
            x.ForceClosed,
            x.ViewMode == NavigatorViewModeType.Tiles,
            x.Kind == NavigatorPublicCategoryKind.StaffPicks,
            [.. x.Rooms.Select(r => r.RoomId)]
        );
}
