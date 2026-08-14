using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using Turbo.Admin.Rooms.Contracts;
using Turbo.Database.Context;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Admin;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Admin.Rooms.Endpoints;

public static class RoomsEndpoints
{
    public static IEndpointRouteBuilder MapAdminRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/rooms").RequireAuthorization();

        group.MapGet(
            "/",
            async (
                string? search,
                int page,
                int pageSize,
                IDbContextFactory<TurboDbContext> dbCtxFactory,
                GrainClientHolder grainClientHolder,
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

                var query = dbCtx.Rooms.AsNoTracking().AsQueryable();

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
                        OwnerName = x.PlayerEntity.Name,
                        x.UsersNow,
                        x.PlayersMax,
                    })
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var activeIds = new HashSet<int>();
                var grainFactory = grainClientHolder.Current;

                if (grainFactory is not null)
                {
                    try
                    {
                        var activeRooms = await grainFactory
                            .GetRoomDirectoryGrain()
                            .GetActiveRoomsAsync()
                            .ConfigureAwait(false);

                        activeIds = [.. activeRooms.Select(x => x.RoomId.Value)];
                    }
                    catch (Exception)
                    {
                        // leave activeIds empty; rooms just show as inactive
                    }
                }

                var items = rows.Select(row => new RoomListItem(
                    row.Id,
                    row.Name,
                    row.OwnerName,
                    row.UsersNow,
                    row.PlayersMax,
                    activeIds.Contains(row.Id)
                ));

                return Results.Ok(new RoomListResponse([.. items], totalCount, page, pageSize));
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
                    .Rooms.AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                        x.Description,
                        OwnerName = x.PlayerEntity.Name,
                        x.DoorMode,
                        x.Password,
                        x.PlayersMax,
                        x.MuteType,
                        x.KickType,
                        x.BanType,
                    })
                    .SingleOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (entity is null)
                    return Results.NotFound();

                var activeRooms = await grainFactory
                    .GetRoomDirectoryGrain()
                    .GetActiveRoomsAsync()
                    .ConfigureAwait(false);

                var isActive = activeRooms.Any(x => x.RoomId.Value == id);

                var live = new RoomLiveInfo(false, 0, []);

                if (isActive)
                {
                    var roomGrain = grainFactory.GetRoomGrain((RoomId)id);

                    var avatars = await roomGrain
                        .GetAllAvatarSnapshotsAsync(ct)
                        .ConfigureAwait(false);

                    var avatarInfos = avatars
                        .Where(x => x.AvatarType == RoomObjectType.Player)
                        .Select(x => new RoomAvatarInfo(x.WebId, x.Name, x.X, x.Y))
                        .ToArray();

                    live = new RoomLiveInfo(true, avatarInfos.Length, avatarInfos);
                }

                return Results.Ok(
                    new RoomDetailResponse(
                        entity.Id,
                        entity.Name,
                        entity.Description ?? string.Empty,
                        entity.OwnerName,
                        entity.DoorMode,
                        !string.IsNullOrEmpty(entity.Password),
                        entity.PlayersMax,
                        entity.MuteType,
                        entity.KickType,
                        entity.BanType,
                        live
                    )
                );
            }
        );

        group.MapPut(
            "/{id:int}",
            async (
                int id,
                UpdateRoomRequest request,
                IGrainFactory grainFactory,
                CancellationToken ct
            ) =>
            {
                var roomGrain = grainFactory.GetRoomGrain((RoomId)id);

                await roomGrain
                    .AdminUpdateSettingsAsync(
                        new RoomAdminSettingsUpdate
                        {
                            Name = request.Name,
                            Description = request.Description,
                            DoorMode = request.DoorMode,
                            Password = request.Password ?? string.Empty,
                            PlayersMax = request.PlayersMax,
                            WhoCanMute = request.WhoCanMute,
                            WhoCanKick = request.WhoCanKick,
                            WhoCanBan = request.WhoCanBan,
                        },
                        ct
                    )
                    .ConfigureAwait(false);

                return Results.NoContent();
            }
        );

        group.MapPost(
            "/{id:int}/kick/{playerId:int}",
            async (int id, int playerId, IGrainFactory grainFactory, CancellationToken ct) =>
            {
                var roomGrain = grainFactory.GetRoomGrain((RoomId)id);

                var removed = await roomGrain
                    .RemoveAvatarFromPlayerAsync(
                        ActionContext.CreateForSystem((RoomId)id),
                        (PlayerId)playerId,
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

                var roomGrain = grainFactory.GetRoomGrain((RoomId)id);

                var avatars = await roomGrain.GetAllAvatarSnapshotsAsync(ct).ConfigureAwait(false);

                var playerIds = avatars
                    .Where(x => x.AvatarType == RoomObjectType.Player)
                    .Select(x => x.WebId)
                    .ToImmutableArray();

                await Task.WhenAll(
                        playerIds.Select(pid =>
                            grainFactory
                                .GetPlayerPresenceGrain(pid)
                                .SendComposerAsync(
                                    new MOTDNotificationEventMessageComposer
                                    {
                                        Messages = [request.Message],
                                    }
                                )
                        )
                    )
                    .ConfigureAwait(false);

                return Results.NoContent();
            }
        );

        return app;
    }
}
