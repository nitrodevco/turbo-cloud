using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Entities.Room;
using Turbo.Database.Extensions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> RateRoomAsync(ActionContext ctx, int points, CancellationToken ct)
    {
        // One vote per player, up or down.
        if (points is not (1 or -1) || !await GetCanRateAsync(ctx.PlayerId, ct))
            return false;

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            dbCtx.RoomRatings.Add(
                new RoomRatingEntity
                {
                    RoomEntityId = _state.RoomId.Value,
                    PlayerEntityId = ctx.PlayerId.Value,
                    Rating = points,
                }
            );

            await dbCtx.SaveChangesAsync(ct);

            await dbCtx
                .Rooms.Where(x => x.Id == _state.RoomId.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Score, x => x.Score + points), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to rate room {RoomId} by player {PlayerId}",
                _state.RoomId,
                ctx.PlayerId
            );

            return false;
        }

        _state.PlayerIdsWhoRated.Add(ctx.PlayerId);
        _state.RoomSnapshot = _state.RoomSnapshot with
        {
            Score = _state.RoomSnapshot.Score + points,
        };

        await PublishToDirectoryAsync(ct);

        await _grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new RoomRatingMessageComposer { Rating = _state.RoomSnapshot.Score, CanRate = false },
            ct
        );

        return true;
    }

    public async Task<bool> GetCanRateAsync(PlayerId playerId, CancellationToken ct) =>
        playerId.Value > 0
        && !_state.PlayerIdsWhoRated.Contains(playerId)
        && !await SecurityModule.GetIsRoomOwnerAsync(playerId);

    public async Task SetStaffPickAsync(bool staffPick, CancellationToken ct)
    {
        if (_state.RoomSnapshot.StaffPick == staffPick)
            return;

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            await dbCtx
                .Rooms.Where(x => x.Id == _state.RoomId.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StaffPick, staffPick), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to set staff pick {StaffPick} for room {RoomId}",
                staffPick,
                _state.RoomId
            );

            return;
        }

        _state.RoomSnapshot = _state.RoomSnapshot with { StaffPick = staffPick };

        await PublishRoomInfoUpdatedAsync(ct);
    }

    public async Task<bool> SetTagsAsync(
        ActionContext ctx,
        ImmutableArray<string> tags,
        CancellationToken ct
    )
    {
        if (!await SecurityModule.GetIsRoomOwnerAsync(ctx))
            return false;

        var value = RoomTags.Join(tags);

        if (value.Length > RoomEntity.TAGS_MAX_LENGTH)
            return false;

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            await dbCtx
                .Rooms.Where(x => x.Id == _state.RoomId.Value)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.Tags, value.Length == 0 ? null : value),
                    ct
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set tags for room {RoomId}", _state.RoomId);

            return false;
        }

        _state.RoomSnapshot = _state.RoomSnapshot with { Tags = tags };

        await PublishRoomInfoUpdatedAsync(ct);

        return true;
    }

    public async Task<bool> RemoveOwnRightsAsync(ActionContext ctx, CancellationToken ct)
    {
        try
        {
            await SecurityModule.EnsureRightsLoadedAsync(ct);

            return await SecurityModule.RemoveOwnRightsAsync(ctx.PlayerId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to remove own rights of player {PlayerId} in room {RoomId}",
                ctx.PlayerId,
                _state.RoomId
            );

            return false;
        }
    }

    public Task<RoomEventSnapshot?> GetActiveEventAsync(CancellationToken ct) =>
        Task.FromResult(GetActiveEvent());

    public async Task<RoomEventSnapshot?> CreateEventAsync(
        PlayerId playerId,
        int categoryId,
        string name,
        string description,
        TimeSpan duration,
        CancellationToken ct
    )
    {
        if (
            duration <= TimeSpan.Zero
            || !IsValidEventText(name, description)
            || !await SecurityModule.GetIsRoomOwnerAsync(playerId)
        )
            return null;

        var now = DateTime.UtcNow;
        var current = GetActiveEvent();
        RoomEventSnapshot next;

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            if (current is not null)
            {
                var expiresAt = current.ExpiresAtUtc + duration;

                await dbCtx
                    .RoomEvents.Where(x => x.Id == current.EventId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, expiresAt), ct);

                next = current with { ExpiresAtUtc = expiresAt };
            }
            else
            {
                var entity = new RoomEventEntity
                {
                    RoomEntityId = _state.RoomId.Value,
                    PlayerEntityId = playerId.Value,
                    NavigatorEventCategoryEntityId = categoryId,
                    Name = name,
                    Description = description,
                    ExpiresAt = now + duration,
                };

                dbCtx.RoomEvents.Add(entity);

                await dbCtx.SaveChangesAsync(ct);

                // The row's created-at is stamped by the database, so the snapshot takes ours.
                next = entity.ToSnapshot(_state.RoomSnapshot.OwnerName) with
                {
                    CreatedAtUtc = now,
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create event for room {RoomId}", _state.RoomId);

            return null;
        }

        _state.RoomSnapshot = _state.RoomSnapshot with { ActiveEvent = next };

        await PublishToDirectoryAsync(ct);
        await SendComposerToRoomAsync(
            new RoomEventMessageComposer { Event = next, SentAtUtc = now },
            ct
        );

        return next;
    }

    public async Task<bool> UpdateEventAsync(
        ActionContext ctx,
        int eventId,
        string name,
        string description,
        CancellationToken ct
    )
    {
        var current = GetActiveEvent();

        if (
            current is null
            || current.EventId != eventId
            || !IsValidEventText(name, description)
            || !await SecurityModule.GetIsRoomOwnerAsync(ctx)
        )
            return false;

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            await dbCtx
                .RoomEvents.Where(x => x.Id == eventId)
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Name, name)
                            .SetProperty(x => x.Description, description),
                    ct
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update event {EventId} in room {RoomId}",
                eventId,
                _state.RoomId
            );

            return false;
        }

        var next = current with { Name = name, Description = description };

        _state.RoomSnapshot = _state.RoomSnapshot with { ActiveEvent = next };

        await PublishToDirectoryAsync(ct);
        await SendComposerToRoomAsync(
            new RoomEventMessageComposer { Event = next, SentAtUtc = DateTime.UtcNow },
            ct
        );

        return true;
    }

    public async Task<bool> CancelEventAsync(ActionContext ctx, int eventId, CancellationToken ct)
    {
        var current = GetActiveEvent();

        if (
            current is null
            || current.EventId != eventId
            || !await SecurityModule.GetIsRoomOwnerAsync(ctx)
        )
            return false;

        var now = DateTime.UtcNow;

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            // Expire rather than delete, so the event stays in the history.
            await dbCtx
                .RoomEvents.Where(x => x.Id == eventId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, now), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to cancel event {EventId} in room {RoomId}",
                eventId,
                _state.RoomId
            );

            return false;
        }

        _state.RoomSnapshot = _state.RoomSnapshot with { ActiveEvent = null };

        await PublishToDirectoryAsync(ct);
        await SendComposerToRoomAsync(new RoomEventCancelMessageComposer(), ct);

        return true;
    }

    private static bool IsValidEventText(string name, string description) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= RoomEventEntity.NAME_MAX_LENGTH
        && description is not null
        && description.Length <= RoomEventEntity.DESCRIPTION_MAX_LENGTH;

    private RoomEventSnapshot? GetActiveEvent()
    {
        var current = _state.RoomSnapshot.ActiveEvent;

        if (current is null)
            return null;

        if (current.IsActiveAt(DateTime.UtcNow))
            return current;

        _state.RoomSnapshot = _state.RoomSnapshot with { ActiveEvent = null };

        return null;
    }

    /// <summary>
    /// The navigator overlays the directory's copy of active rooms on its cached database rows,
    /// so every change to navigator-visible data is pushed there straight away.
    /// </summary>
    private Task PublishToDirectoryAsync(CancellationToken ct)
    {
        _state.IsListingChanged = true;

        return _grainFactory.GetRoomDirectoryGrain().UpsertActiveRoomAsync(_state.RoomSnapshot, ct);
    }

    private async Task PublishRoomInfoUpdatedAsync(CancellationToken ct)
    {
        await PublishToDirectoryAsync(ct);
        await SendComposerToRoomAsync(
            new RoomInfoUpdatedMessageComposer { RoomId = _state.RoomId },
            ct
        );
    }
}
