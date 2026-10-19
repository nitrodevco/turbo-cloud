using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;

namespace Turbo.Operations;

/// <summary>
/// The lists of <c>:uc</c> and <c>:anew</c> (<c>ChatInputWidgetHandler</c>). Who registered when
/// and their names come from one query per list; Habbo Club is the subscription grain's answer
/// (<c>IGrainFactory.HasActiveClubAsync</c>, which counts a club held by permission), asked only
/// of the players it can change the class of.
/// </summary>
public sealed class UserClassificationService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    TimeProvider timeProvider,
    IOptions<OperationsConfig> config,
    ILogger<UserClassificationService> logger
) : IUserClassificationService
{
    public async Task<ImmutableArray<UserClassificationSnapshot>?> ClassifyRoomAsync(
        PlayerId caller,
        RoomId roomId,
        string classification,
        CancellationToken ct
    )
    {
        var filter = classification.Trim();

        // ChatInputWidgetHandler.as:403-421: :uc is sent at security level 4 alone, and an
        // ambassador's only way here is :anew, which asks for new users.
        if (
            !string.Equals(filter, UserClassifications.NEW, StringComparison.OrdinalIgnoreCase)
            && !await grainFactory.HasPermissionAsync(caller, PermissionNodes.Room.MODERATE_ANY, ct)
        )
        {
            logger.LogWarning(
                "Refused player {PlayerId} the users of class {Classification} in room {RoomId}: without {Node} only new users are listed",
                caller,
                filter,
                roomId,
                PermissionNodes.Room.MODERATE_ANY
            );

            return null;
        }

        var players = await grainFactory.GetRoomDirectoryGrain().GetRoomPlayersAsync(roomId, ct);

        return await ClassifyAsync(players, filter, ct);
    }

    public Task<ImmutableArray<UserClassificationSnapshot>> ClassifyHotelAsync(
        string classification,
        CancellationToken ct
    ) => ClassifyAsync(sessionGateway.GetOnlinePlayerIds(), classification.Trim(), ct);

    private async Task<ImmutableArray<UserClassificationSnapshot>> ClassifyAsync(
        IEnumerable<PlayerId> candidates,
        string filter,
        CancellationToken ct
    )
    {
        var max = config.Value.UserClassificationMaxUsers;

        if (max <= 0 || !UserClassifications.IsFilter(filter))
            return [];

        var newUserAge = TimeSpan.FromDays(config.Value.UserClassificationNewUserDays);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Club only tells paying from regular, so a list of new users never asks it.
        var asksClub = !string.Equals(
            filter,
            UserClassifications.NEW,
            StringComparison.OrdinalIgnoreCase
        );
        var listed = new List<UserClassificationSnapshot>();

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        // A list is read a window's worth of players at a time, and ends once it is full, so a
        // hotel with many online is not read whole for a list it cannot show.
        foreach (var chunk in candidates.Distinct().Order().Chunk(max))
        {
            var ids = chunk.Select(x => x.Value).ToList();
            var players = await dbCtx
                .Players.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.CreatedAt,
                })
                .ToListAsync(ct);

            var clubs = await Task.WhenAll(
                players.Select(x =>
                    asksClub && !UserClassifications.IsNew(x.CreatedAt, now, newUserAge)
                        ? grainFactory.HasActiveClubAsync(x.Id, ct)
                        : Task.FromResult(false)
                )
            );

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var classType = UserClassifications.Classify(
                    player.CreatedAt,
                    clubs[i],
                    now,
                    newUserAge
                );

                if (!UserClassifications.Matches(filter, classType))
                    continue;

                listed.Add(
                    new UserClassificationSnapshot
                    {
                        UserId = player.Id,
                        UserName = player.Name,
                        ClassType = classType,
                    }
                );
            }

            if (listed.Count >= max)
                break;
        }

        return
        [
            .. listed
                .OrderBy(x => x.UserId)
                .Take(max)
                .OrderBy(x => x.UserName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.UserId),
        ];
    }
}
