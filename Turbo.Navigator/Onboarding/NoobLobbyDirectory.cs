using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Navigator.Onboarding;

/// <summary>
/// <see cref="INoobLobbyDirectory"/>: the rooms whose door mode is
/// <see cref="RoomDoorModeType.NoobLobby"/>, with their head counts from the room directory.
/// </summary>
public sealed class NoobLobbyDirectory(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    IGamedataVariableService variables
) : INoobLobbyDirectory
{
    /// <summary>Flash's switch for the lobby offer, the toolbar's way to the lobby and noob rooms.</summary>
    public const string LOBBIES_ENABLED_VARIABLE = "nux.lobbies.enabled";

    public async Task<ImmutableArray<NoobLobbySnapshot>> ListAsync(CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var rooms = await db
            .Rooms.AsNoTracking()
            .Where(x => x.DoorMode == RoomDoorModeType.NoobLobby)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.PlayersMax,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var directory = grainFactory.GetRoomDirectoryGrain();
        var lobbies = ImmutableArray.CreateBuilder<NoobLobbySnapshot>(rooms.Count);

        foreach (var room in rooms)
        {
            var players = await directory
                .GetRoomPopulationAsync(new RoomId(room.Id), ct)
                .ConfigureAwait(false);

            lobbies.Add(new(new RoomId(room.Id), room.Name, players, room.PlayersMax));
        }

        return lobbies.ToImmutable();
    }

    public async Task<RoomId?> PickAsync(CancellationToken ct)
    {
        var open = (await ListAsync(ct).ConfigureAwait(false))
            .Where(x => x.PlayersMax <= 0 || x.Players < x.PlayersMax)
            .ToList();

        if (open.Count == 0)
            return null;

        var most = open.Max(x => x.Players);
        var fullest = open.Where(x => x.Players == most).ToList();

        return fullest[Random.Shared.Next(fullest.Count)].RoomId;
    }

    public async Task<NoobLobbyChange> SetAsync(
        RoomId roomId,
        bool isLobby,
        PlayerId by,
        CancellationToken ct
    )
    {
        var changed = await grainFactory
            .GetRoomGrain(roomId)
            .SetNoobLobbyAsync(isLobby, ct)
            .ConfigureAwait(false);

        if (!changed)
            return isLobby ? NoobLobbyChange.AlreadyAdded : NoobLobbyChange.NotALobby;

        var any = !(await ListAsync(ct).ConfigureAwait(false)).IsEmpty;

        await variables
            .SaveAsync(LOBBIES_ENABLED_VARIABLE, JsonSerializer.Serialize(any), by, ct)
            .ConfigureAwait(false);

        return isLobby ? NoobLobbyChange.Added : NoobLobbyChange.Removed;
    }
}
