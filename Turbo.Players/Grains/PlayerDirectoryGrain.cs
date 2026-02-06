using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;

namespace Turbo.Players.Grains;

[KeepAlive]
internal class PlayerDirectoryGrain(IDbContextFactory<TurboDbContext> dbCtxFactory)
    : Grain,
        IPlayerDirectoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;

    private readonly Dictionary<PlayerId, string> _idToName = [];
    private readonly Dictionary<string, PlayerId> _nameToId = new(
        System.StringComparer.OrdinalIgnoreCase
    );

    public async Task<string> GetPlayerNameAsync(PlayerId playerId, CancellationToken ct)
    {
        if (_idToName.TryGetValue(playerId, out var x))
            return x;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var dbName = await dbCtx
            .Players.AsNoTracking()
            .Where(x => x.Id == (int)playerId)
            .Select(x => x.Name)
            .FirstAsync(ct);

        if (string.IsNullOrWhiteSpace(dbName))
            return string.Empty;

        SetCache(playerId, dbName);

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
                if (_idToName.TryGetValue(playerId, out var name))
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
                    SetCache(player.Key, player.Value);

                    names.TryAdd(player.Key, player.Value);
                }
            }
        }

        return names.ToImmutableDictionary();
    }

    public Task SetPlayerNameAsync(PlayerId playerId, string name, CancellationToken ct)
    {
        SetCache(playerId, name);

        return Task.CompletedTask;
    }

    public Task InvalidatePlayerNameAsync(PlayerId playerId, CancellationToken ct)
    {
        if (_idToName.Remove(playerId, out var oldName))
            _nameToId.Remove(oldName);

        return Task.CompletedTask;
    }

    public async Task<PlayerId?> GetPlayerIdAsync(string userName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return null;

        var normalizedUserName = userName.Trim();
        if (_nameToId.TryGetValue(normalizedUserName, out var cachedPlayerId))
            return cachedPlayerId;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var loweredUserName = normalizedUserName.ToLowerInvariant();
        var player = await dbCtx
            .Players.AsNoTracking()
            .Where(x => x.Name != null && x.Name.ToLower() == loweredUserName)
            .Select(x => new { x.Id, x.Name })
            .FirstOrDefaultAsync(ct);

        if (player is null or { Id: <= 0 })
            return null;

        var playerId = (PlayerId)player.Id;
        SetCache(playerId, player.Name ?? normalizedUserName);

        return playerId;
    }

    private void SetCache(PlayerId playerId, string name)
    {
        if (_idToName.TryGetValue(playerId, out var existingName))
            _nameToId.Remove(existingName);

        _idToName[playerId] = name;
        _nameToId[name] = playerId;
    }
}
