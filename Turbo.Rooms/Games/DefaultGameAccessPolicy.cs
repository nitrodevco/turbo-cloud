using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Primitives.Games;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Games;

/// <summary>
/// The server's own games-left count: <see cref="GameCenterConfig.FreeGamesPerDay"/> (or
/// <see cref="GameCenterConfig.ClubFreeGamesPerDay"/> for a Habbo Club member) less the games the
/// player started today. A plugin's <see cref="IGameAccessPolicy"/> replaces it.
/// </summary>
public sealed class DefaultGameAccessPolicy(
    IOptionsMonitor<GameCenterConfig> config,
    IGrainFactory grainFactory,
    GameScoreStore scores
) : IGameAccessPolicy
{
    public async Task<int> GetFreeGamesLeftAsync(
        PlayerId playerId,
        int gameTypeId,
        CancellationToken ct
    )
    {
        var settings = config.CurrentValue;
        var perDay = await grainFactory.HasActiveClubAsync(playerId, ct).ConfigureAwait(false)
            ? settings.ClubFreeGamesPerDay
            : settings.FreeGamesPerDay;

        if (perDay < 0)
            return GameCenter.UNLIMITED;

        var (_, today) = await scores
            .CountGamesAsync(playerId, gameTypeId, ct)
            .ConfigureAwait(false);

        return Math.Max(0, perDay - today);
    }
}
