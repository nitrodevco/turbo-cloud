using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Common;
using Turbo.Admin.Configuration;
using Turbo.Admin.Users.Contracts;
using Turbo.Database.Context;
using Turbo.Primitives.Action;
using Turbo.Primitives.Grains.Players;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Users.Endpoints;

internal static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/users").RequireAuthorization();

        group.MapGet(
            "/",
            async (
                string? search,
                int page,
                int pageSize,
                IDbContextFactory<TurboDbContext> dbCtxFactory,
                IGrainFactory grainFactory,
                IOptions<AdminConfig> config,
                CancellationToken ct
            ) =>
            {
                page = page <= 0 ? 1 : page;
                pageSize =
                    pageSize is <= 0 || pageSize > config.Value.MaxPageSize
                        ? config.Value.DefaultPageSize
                        : pageSize;

                await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

                var query = dbCtx.Players.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(x => x.Name.Contains(search));

                var totalCount = await query.CountAsync(ct).ConfigureAwait(false);

                var rows = await query
                    .OrderBy(x => x.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                        x.Motto,
                        x.Figure,
                        x.CreatedAt,
                    })
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var onlineFlags = await Task.WhenAll(
                        rows.Select(async row =>
                        {
                            var presence = grainFactory.GetPlayerPresenceGrain(row.Id);

                            return await presence.HasActiveSessionAsync().ConfigureAwait(false);
                        })
                    )
                    .ConfigureAwait(false);

                var items = rows.Zip(
                    onlineFlags,
                    (row, isOnline) =>
                        new UserListItem(
                            row.Id,
                            row.Name,
                            row.Motto ?? string.Empty,
                            row.Figure,
                            isOnline,
                            row.CreatedAt
                        )
                );

                return Results.Ok(new UserListResponse([.. items], totalCount, page, pageSize));
            }
        );

        group.MapGet(
            "/{id:int}",
            async (
                int id,
                IDbContextFactory<TurboDbContext> dbCtxFactory,
                IGrainFactory grainFactory,
                CancellationToken ct
            ) =>
            {
                await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

                var entity = await dbCtx
                    .Players.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == id, ct)
                    .ConfigureAwait(false);

                if (entity is null)
                    return Results.NotFound();

                var presence = grainFactory.GetPlayerPresenceGrain(id);
                var isOnline = await presence.HasActiveSessionAsync().ConfigureAwait(false);

                UserLiveInfo? live = null;

                if (isOnline)
                {
                    var roomPointer = await presence.GetActiveRoomAsync().ConfigureAwait(false);
                    var inRoom = roomPointer.RoomId.Value > 0;

                    live = new UserLiveInfo(
                        true,
                        inRoom ? roomPointer.RoomId.Value : null,
                        inRoom ? roomPointer.ActiveSinceUtc : null
                    );
                }

                return Results.Ok(
                    new UserDetailResponse(
                        entity.Id,
                        entity.Name,
                        entity.Motto ?? string.Empty,
                        entity.Figure,
                        entity.Gender,
                        entity.PlayerPerks,
                        entity.CreatedAt,
                        live ?? new UserLiveInfo(false, null, null)
                    )
                );
            }
        );

        group.MapPut(
            "/{id:int}",
            async (
                int id,
                UpdateUserRequest request,
                IGrainFactory grainFactory,
                CancellationToken ct
            ) =>
            {
                var grain = grainFactory.GetPlayerGrain(id);

                if (request.Name is not null)
                    await grainFactory
                        .GetPlayerDirectoryGrain()
                        .SetPlayerNameAsync(id, request.Name, ct)
                        .ConfigureAwait(false);

                if (request.Motto is not null)
                    await grain.SetMottoAsync(request.Motto, ct).ConfigureAwait(false);

                if (request.Figure is not null && request.Gender is not null)
                    await grain
                        .SetFigureAsync(request.Figure, request.Gender.Value, ct)
                        .ConfigureAwait(false);

                if (request.PlayerPerks is not null)
                    await grain
                        .SetPlayerPerksAsync(request.PlayerPerks.Value, ct)
                        .ConfigureAwait(false);

                return Results.NoContent();
            }
        );

        group.MapPost(
            "/{id:int}/kick",
            async (int id, IGrainFactory grainFactory, CancellationToken ct) =>
            {
                var presence = grainFactory.GetPlayerPresenceGrain(id);
                var roomPointer = await presence.GetActiveRoomAsync().ConfigureAwait(false);

                if (roomPointer.RoomId.Value <= 0)
                    return Results.BadRequest(new { error = "Player is not currently in a room." });

                var roomGrain = grainFactory.GetRoomGrain(roomPointer.RoomId);

                var removed = await roomGrain
                    .RemoveAvatarFromPlayerAsync(
                        ActionContext.CreateForSystem(roomPointer.RoomId),
                        (PlayerId)id,
                        ct
                    )
                    .ConfigureAwait(false);

                return removed ? Results.NoContent() : Results.BadRequest();
            }
        );

        group.MapPost(
            "/{id:int}/alert",
            async (
                int id,
                AlertRequest request,
                IGrainFactory grainFactory,
                CancellationToken ct
            ) =>
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                    return Results.BadRequest(new { error = "Message is required." });

                var presence = grainFactory.GetPlayerPresenceGrain(id);

                if (!await presence.HasActiveSessionAsync().ConfigureAwait(false))
                    return Results.BadRequest(new { error = "Player is not currently online." });

                await presence
                    .SendComposerAsync(
                        new MOTDNotificationEventMessageComposer { Messages = [request.Message] }
                    )
                    .ConfigureAwait(false);

                return Results.NoContent();
            }
        );

        return app;
    }
}
