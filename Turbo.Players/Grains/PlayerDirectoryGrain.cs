using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;

namespace Turbo.Players.Grains;

/// <summary>
/// Player names and ids for the whole hotel, one grain. It is a read-through cache over the
/// players table: nothing here is written back, so there is nothing to flush on deactivation.
/// </summary>
[KeepAlive]
internal sealed class PlayerDirectoryGrain : Grain, IPlayerDirectoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly ILogger<IPlayerDirectoryGrain> _logger;

    private readonly PlayerDirectoryLiveState _state = new();

    public PlayerDirectoryGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        ILogger<IPlayerDirectoryGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _logger = logger;
    }

    public async Task<string> GetPlayerNameAsync(PlayerId playerId, CancellationToken ct)
    {
        if (_state.IdToName.TryGetValue(playerId, out var x))
            return x;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var dbName = await dbCtx
            .Players.AsNoTracking()
            .Where(x => x.Id == (int)playerId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(dbName))
        {
            // Asking for a player who is not there is a caller bug, not an outage.
            _logger.LogWarning("No name found for player {PlayerId}", playerId);

            return string.Empty;
        }

        SetNameCache(playerId, dbName);

        return dbName;
    }

    public async Task<ImmutableDictionary<PlayerId, string>> GetPlayerNamesAsync(
        List<PlayerId> playerIds,
        CancellationToken ct
    )
    {
        var names = new Dictionary<PlayerId, string>();

        if (playerIds.Count == 1)
        {
            var singleId = playerIds[0];
            var singleName = await GetPlayerNameAsync(singleId, ct);

            names.TryAdd(singleId, singleName);
        }
        else
        {
            var ids = playerIds.Distinct().ToList();
            var notFound = new List<PlayerId>();

            foreach (var playerId in ids)
            {
                if (_state.IdToName.TryGetValue(playerId, out var name))
                {
                    names.TryAdd(playerId, name);
                }
                else
                {
                    notFound.Add(playerId);
                }
            }

            if (notFound.Count > 0)
            {
                await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

                var players = await dbCtx
                    .Players.AsNoTracking()
                    .Where(x => notFound.Select(x => (int)x).Contains(x.Id))
                    .Select(x => new { x.Id, x.Name })
                    .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

                foreach (var player in players)
                {
                    SetNameCache(player.Key, player.Value);

                    names.TryAdd(player.Key, player.Value);
                }
            }
        }

        return names.ToImmutableDictionary();
    }

    public Task SetPlayerNameAsync(PlayerId playerId, string name, CancellationToken ct)
    {
        SetNameCache(playerId, name);

        return Task.CompletedTask;
    }

    public async Task<PlayerId?> GetPlayerIdAsync(string name, CancellationToken ct)
    {
        name = name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (_state.NameToId.TryGetValue(name, out var playerId))
            return playerId;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var player = await dbCtx
            .Players.AsNoTracking()
            .Where(x => x.Name.ToLower().Equals(name.ToLower()))
            .Select(x => new { x.Id, x.Name })
            .FirstOrDefaultAsync(ct);

        if (player is null)
            return null;

        playerId = PlayerId.Parse(player.Id);

        SetNameCache(playerId, player.Name);

        return playerId;
    }

    private void SetNameCache(PlayerId playerId, string name)
    {
        if (_state.IdToName.TryGetValue(playerId, out var existingName))
            _state.NameToId.Remove(existingName);

        _state.IdToName[playerId] = name;
        _state.NameToId[name] = playerId;
    }
}
