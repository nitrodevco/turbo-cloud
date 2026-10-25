using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Database.Context;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;

namespace Turbo.Main.Hotel;

/// <summary>
/// <see cref="IHotelRooms"/> over the rooms' table (read only), the navigator's public list, the
/// room directory (who is where, without loading a room) and the room grains.
/// </summary>
internal sealed class HostHotelRooms(
    IDbContextFactory<TurboDbContext> database,
    IGrainFactory grains,
    INavigatorService navigator
) : IHotelRooms
{
    public async Task<HotelRoom?> GetAsync(RoomId room, CancellationToken ct)
    {
        await using var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);

        var row = await db
            .Rooms.AsNoTracking()
            .Where(x => x.Id == room.Value)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.PlayerEntityId,
                OwnerName = x.PlayerEntity.Name,
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (row is null)
            return null;

        var isPublic = (await PublicIdsAsync(ct).ConfigureAwait(false)).Contains(room);

        return new HotelRoom(
            new RoomId(row.Id),
            row.Name,
            row.Description ?? string.Empty,
            new PlayerId(row.PlayerEntityId),
            row.OwnerName,
            isPublic
        );
    }

    public async Task<IReadOnlyCollection<RoomId>> PublicIdsAsync(CancellationToken ct) =>
        [
            .. (await navigator.GetOfficialRoomsAsync(ct).ConfigureAwait(false))
                .Select(x => x.RoomId)
                .Distinct(),
        ];

    public async Task<IReadOnlyList<PlayerId>> PlayersAsync(RoomId room, CancellationToken ct) =>
        await grains.GetRoomDirectoryGrain().GetRoomPlayersAsync(room, ct).ConfigureAwait(false);

    public Task<bool> SendAsync(RoomId room, IPluginMessage message, CancellationToken ct) =>
        SendAsync(room, PluginMessageComposer.For(message), ct);

    /// <summary>
    /// The room's own send: one write for everyone in it. A room no one is in is left alone
    /// rather than loaded for it.
    /// </summary>
    public async Task<bool> SendAsync(RoomId room, IComposer composer, CancellationToken ct)
    {
        if (
            await grains
                .GetRoomDirectoryGrain()
                .GetRoomPopulationAsync(room, ct)
                .ConfigureAwait(false) <= 0
        )
            return false;

        await grains.GetRoomGrain(room).SendComposerToRoomAsync(composer, ct).ConfigureAwait(false);

        return true;
    }

    /// <summary>Like a send, a room no one is in is not loaded for it.</summary>
    public async Task<bool> WhisperAsync(
        RoomId room,
        PlayerId player,
        string text,
        CancellationToken ct
    ) =>
        await grains.GetRoomDirectoryGrain().GetRoomPopulationAsync(room, ct).ConfigureAwait(false)
            > 0
        && await grains
            .GetRoomGrain(room)
            .WhisperToPlayerAsync(player, text, ct)
            .ConfigureAwait(false);

    public Task LoadAsync(RoomId room, CancellationToken ct) =>
        grains.GetRoomGrain(room).EnsureRoomActiveAsync(ct);
}
