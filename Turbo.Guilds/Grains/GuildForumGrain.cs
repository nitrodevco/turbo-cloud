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
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Extensions;
using Turbo.Guilds.Configuration;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Forums;
using Turbo.Primitives.Guilds.Forums.Enums;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Guilds.Grains;

/// <summary>
/// A group's forum, write-through: every post, moderation and settings change is saved before
/// it is answered. The grain holds the forum's row (settings and counters) and numbers new
/// messages within the forum, one at a time; threads and messages are read a page at a time.
/// What a player may do follows the AS3 forum views: a permission is everybody, members, admins
/// or the owner (groupforum.permissions.option_*), a refusal is the
/// groupforum.view.error.&lt;error&gt; the client shows, staff (the moderation tool) may do
/// everything, and hidden posts are masked for whoever may not see them.
/// </summary>
internal sealed class GuildForumGrain : Grain, IGuildForumGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly GuildConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IGuildForumGrain> _logger;

    private readonly GuildForumLiveState _state;

    public GuildForumGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<GuildConfig> config,
        IGrainFactory grainFactory,
        ILogger<IGuildForumGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = new() { GuildId = this.GetGuildId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            _state.Forum = await dbCtx
                .GuildForums.AsNoTracking()
                .FirstOrDefaultAsync(x => x.GuildEntityId == _state.GuildId.Value, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate the forum of group {GuildId}", _state.GuildId);

            throw;
        }
    }

    public async Task<bool> OpenAsync(PlayerId buyer, CancellationToken ct)
    {
        if (_state.Forum is not null)
            return false;

        var guild = await _grainFactory.GetGuildGrain(_state.GuildId).GetSnapshotAsync(ct);

        if (guild is null || guild.OwnerId != buyer)
        {
            _logger.LogWarning(
                "Forum terminal for group {GuildId} bought by player {PlayerId}, who does not own it: no forum opened",
                _state.GuildId,
                buyer
            );

            return false;
        }

        var forum = new GuildForumEntity { GuildEntityId = _state.GuildId.Value };

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            dbCtx.GuildForums.Add(forum);
            await dbCtx.SaveChangesAsync(ct);
            dbCtx.Entry(forum).State = EntityState.Detached;
        }

        _state.Forum = forum;

        await _grainFactory.GetGuildGrain(_state.GuildId).OnForumOpenedAsync(ct);
        await NotifyAsync(
            buyer,
            GuildForumNotificationTypes.DELIVERED,
            ImmutableDictionary<string, string>
                .Empty.Add("GROUPNAME", guild.Name)
                .Add("GROUPID", _state.GuildId.Value.ToString()),
            ct
        );

        return true;
    }

    public async Task SendForumAsync(PlayerId viewer, CancellationToken ct)
    {
        if (_state.Forum is null)
        {
            await DenyAsync(viewer, "open a group without a forum", ct);

            return;
        }

        var access = await AccessAsync(viewer, ct);

        await SendAsync(
            viewer,
            new ForumDataMessageComposer { Forum = await DetailAsync(viewer, access, ct) },
            ct
        );
    }

    public async Task SendThreadsAsync(
        PlayerId viewer,
        int startIndex,
        int amount,
        CancellationToken ct
    )
    {
        if (await ReaderAsync(viewer, ct) is not { } access)
            return;

        startIndex = Math.Max(0, startIndex);
        amount = Math.Clamp(amount, 0, _config.ForumPageSize);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var threads = await dbCtx
            .GuildForumThreads.AsNoTracking()
            .Where(x => x.GuildEntityId == _state.GuildId.Value)
            .OrderByDescending(x => x.IsSticky)
            .ThenByDescending(x => x.LastMessageAt)
            .ThenByDescending(x => x.Id)
            .Skip(startIndex)
            .Take(amount)
            .ToListAsync(ct);

        await SendAsync(
            viewer,
            new ForumThreadsMessageComposer
            {
                GroupId = _state.GuildId.Value,
                StartIndex = startIndex,
                Threads = await ThreadSnapshotsAsync(dbCtx, threads, viewer, access, ct),
            },
            ct
        );
    }

    public async Task SendThreadAsync(PlayerId viewer, int threadId, CancellationToken ct)
    {
        if (await ReaderAsync(viewer, ct) is not { } access)
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var thread = await FindThreadAsync(dbCtx, threadId, ct);

        if (thread is null)
            return;

        await SendAsync(
            viewer,
            new UpdateThreadMessageComposer
            {
                GroupId = _state.GuildId.Value,
                Thread = (await ThreadSnapshotsAsync(dbCtx, [thread], viewer, access, ct))[0],
            },
            ct
        );
    }

    public async Task SendMessagesAsync(
        PlayerId viewer,
        int threadId,
        int startIndex,
        int amount,
        CancellationToken ct
    )
    {
        if (await ReaderAsync(viewer, ct) is not { } access)
            return;

        startIndex = Math.Max(0, startIndex);
        amount = Math.Clamp(amount, 0, _config.ForumPageSize);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        // A hidden thread's messages keep their own state, so the thread's is what hides them: the
        // list shows its subject blank to whoever may not see it, and its messages stay closed too.
        if (
            await FindThreadAsync(dbCtx, threadId, ct) is not { } thread
            || !access.MaySee(thread.State, _state.Forum!.ModeratePermission)
        )
            return;

        var messages = await dbCtx
            .GuildForumMessages.AsNoTracking()
            .Where(x => x.ThreadEntityId == threadId)
            .OrderBy(x => x.ThreadIndex)
            .Skip(startIndex)
            .Take(amount)
            .ToListAsync(ct);

        await SendAsync(
            viewer,
            new ThreadMessagesMessageComposer
            {
                GroupId = _state.GuildId.Value,
                ThreadId = threadId,
                StartIndex = startIndex,
                Messages = await MessageSnapshotsAsync(dbCtx, messages, access, ct),
            },
            ct
        );

        if (messages.Count == 0)
            return;

        // Reading a thread marks its messages read: the AS3 client never sends
        // UpdateForumReadMarker, and on Habbo opening a thread drops My Forums and the me menu's
        // forum counter (4 -> 3, forum-messages.png). The marker is the forum's, so it moves to
        // the last message shown (inference: one marker per forum, as the protocol has).
        var player = _grainFactory.GetPlayerGuildForumGrain(viewer);

        await player.MarkReadAsync(
            [
                new GuildForumReadMarkerSnapshot
                {
                    GroupId = _state.GuildId.Value,
                    LastReadMessageId = messages.Max(x => x.ForumMessageId),
                    MarkAll = false,
                },
            ],
            ct
        );
        await player.SendUnreadForumsCountAsync(ct);
    }

    public async Task PostAsync(
        PlayerId author,
        int threadId,
        string subject,
        string text,
        CancellationToken ct
    )
    {
        if (_state.Forum is not { } forum)
            return;

        var access = await AccessAsync(author, ct);
        var now = DateTime.UtcNow;
        text = (text ?? "").Trim();
        subject = (subject ?? "").Trim();

        if (
            text.Length < _config.ForumMessageMinLength
            || text.Length > GuildForumMessageEntity.TEXT_MAX_LENGTH
            || (
                threadId == 0
                && (
                    subject.Length < _config.ForumSubjectMinLength
                    || subject.Length > GuildForumThreadEntity.SUBJECT_MAX_LENGTH
                )
            )
        )
        {
            _logger.LogWarning(
                "Rejected forum post by player {PlayerId} in group {GuildId}: subject of {SubjectLength} or text of {TextLength} characters",
                author,
                _state.GuildId,
                subject.Length,
                text.Length
            );

            return;
        }

        if (
            _state.LastPostAtByPlayer.TryGetValue(author.Value, out var last)
            && now - last < TimeSpan.FromMilliseconds(_config.ForumPostCooldownMs)
        )
        {
            _logger.LogWarning(
                "Rejected forum post by player {PlayerId} in group {GuildId}: within the post cooldown",
                author,
                _state.GuildId
            );

            return;
        }

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        GuildForumThreadEntity thread;

        if (threadId == 0)
        {
            if (access.Error(forum.PostThreadPermission) != GuildForumPermissionErrors.NONE)
            {
                await DenyAsync(author, "start a thread", ct);

                return;
            }

            thread = new GuildForumThreadEntity
            {
                GuildEntityId = _state.GuildId.Value,
                PlayerEntityId = author.Value,
                Subject = subject,
                LastMessageAt = now,
            };
            dbCtx.GuildForumThreads.Add(thread);
            forum.ThreadCount++;
        }
        else
        {
            var found = await FindThreadAsync(dbCtx, threadId, ct, tracked: true);

            if (
                found is null
                || access.Error(forum.PostMessagePermission) != GuildForumPermissionErrors.NONE
                || (found.IsLocked && !access.Can(forum.ModeratePermission))
                || (!access.MaySee(found.State, forum.ModeratePermission))
            )
            {
                await DenyAsync(author, "reply in thread " + threadId, ct);

                return;
            }

            thread = found;
            thread.LastMessageAt = now;
        }

        forum.MessageCount++;
        forum.LastMessagePlayerEntityId = author.Value;
        forum.LastMessageAt = now;

        var message = new GuildForumMessageEntity
        {
            GuildEntityId = _state.GuildId.Value,
            Thread = thread,
            ThreadEntityId = thread.Id,
            ForumMessageId = forum.MessageCount,
            ThreadIndex = thread.MessageCount,
            PlayerEntityId = author.Value,
            Text = text,
        };

        thread.MessageCount++;
        dbCtx.GuildForumMessages.Add(message);
        dbCtx.GuildForums.Update(forum);

        try
        {
            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save a post by player {PlayerId} in the forum of group {GuildId}",
                author,
                _state.GuildId
            );

            // The row was not written: take the counters back so the next post numbers right.
            _state.Forum = await dbCtx
                .GuildForums.AsNoTracking()
                .FirstOrDefaultAsync(x => x.GuildEntityId == _state.GuildId.Value, ct);

            throw;
        }

        dbCtx.Entry(forum).State = EntityState.Detached;
        _state.LastPostAtByPlayer[author.Value] = now;

        if (threadId == 0)
            await SendAsync(
                author,
                new PostThreadMessageComposer
                {
                    GroupId = _state.GuildId.Value,
                    Thread = (await ThreadSnapshotsAsync(dbCtx, [thread], author, access, ct))[0],
                },
                ct
            );
        else
            await SendAsync(
                author,
                new PostMessageMessageComposer
                {
                    GroupId = _state.GuildId.Value,
                    ThreadId = thread.Id,
                    Message = (await MessageSnapshotsAsync(dbCtx, [message], access, ct))[0],
                },
                ct
            );
    }

    public async Task ModerateThreadAsync(
        PlayerId actor,
        int threadId,
        int state,
        CancellationToken ct
    )
    {
        if (_state.Forum is not { } forum)
            return;

        var access = await AccessAsync(actor, ct);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var thread = await FindThreadAsync(dbCtx, threadId, ct, tracked: true);

        if (thread is null || !access.MayModerate(thread.State, state, forum.ModeratePermission))
        {
            await DenyAsync(actor, $"set thread {threadId} to state {state}", ct);

            return;
        }

        thread.State = (GuildForumState)state;
        thread.ModeratorEntityId = actor.Value;
        thread.ModeratedAt = DateTime.UtcNow;
        await dbCtx.SaveChangesAsync(ct);

        await SendAsync(
            actor,
            new UpdateThreadMessageComposer
            {
                GroupId = _state.GuildId.Value,
                Thread = (await ThreadSnapshotsAsync(dbCtx, [thread], actor, access, ct))[0],
            },
            ct
        );
        await NotifyAsync(
            actor,
            thread.State == GuildForumState.Restored
                ? GuildForumNotificationTypes.THREAD_RESTORED
                : GuildForumNotificationTypes.THREAD_HIDDEN,
            ct
        );
    }

    public async Task ModerateMessageAsync(
        PlayerId actor,
        int threadId,
        int messageId,
        int state,
        CancellationToken ct
    )
    {
        if (_state.Forum is not { } forum)
            return;

        var access = await AccessAsync(actor, ct);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var message = await dbCtx.GuildForumMessages.FirstOrDefaultAsync(
            x =>
                x.GuildEntityId == _state.GuildId.Value
                && x.ThreadEntityId == threadId
                && x.ForumMessageId == messageId,
            ct
        );

        if (message is null || !access.MayModerate(message.State, state, forum.ModeratePermission))
        {
            await DenyAsync(actor, $"set message {messageId} to state {state}", ct);

            return;
        }

        message.State = (GuildForumState)state;
        message.ModeratorEntityId = actor.Value;
        message.ModeratedAt = DateTime.UtcNow;
        await dbCtx.SaveChangesAsync(ct);

        await SendAsync(
            actor,
            new UpdateMessageMessageComposer
            {
                GroupId = _state.GuildId.Value,
                ThreadId = threadId,
                Message = (await MessageSnapshotsAsync(dbCtx, [message], access, ct))[0],
            },
            ct
        );
        await NotifyAsync(
            actor,
            message.State == GuildForumState.Restored
                ? GuildForumNotificationTypes.MESSAGE_RESTORED
                : GuildForumNotificationTypes.MESSAGE_HIDDEN,
            ct
        );
    }

    public async Task UpdateThreadAsync(
        PlayerId actor,
        int threadId,
        bool isSticky,
        bool isLocked,
        CancellationToken ct
    )
    {
        if (_state.Forum is not { } forum)
            return;

        var access = await AccessAsync(actor, ct);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var thread = await FindThreadAsync(dbCtx, threadId, ct, tracked: true);

        if (thread is null || !access.Can(forum.ModeratePermission))
        {
            await DenyAsync(actor, $"pin or lock thread {threadId}", ct);

            return;
        }

        var notices = new List<string>();

        if (thread.IsLocked != isLocked)
            notices.Add(
                isLocked
                    ? GuildForumNotificationTypes.THREAD_LOCKED
                    : GuildForumNotificationTypes.THREAD_UNLOCKED
            );

        if (thread.IsSticky != isSticky)
            notices.Add(
                isSticky
                    ? GuildForumNotificationTypes.THREAD_PINNED
                    : GuildForumNotificationTypes.THREAD_UNPINNED
            );

        thread.IsSticky = isSticky;
        thread.IsLocked = isLocked;
        await dbCtx.SaveChangesAsync(ct);

        await SendAsync(
            actor,
            new UpdateThreadMessageComposer
            {
                GroupId = _state.GuildId.Value,
                Thread = (await ThreadSnapshotsAsync(dbCtx, [thread], actor, access, ct))[0],
            },
            ct
        );

        foreach (var notice in notices)
            await NotifyAsync(actor, notice, ct);
    }

    public async Task UpdateSettingsAsync(
        PlayerId actor,
        int readPermission,
        int postMessagePermission,
        int postThreadPermission,
        int moderatePermission,
        CancellationToken ct
    )
    {
        if (_state.Forum is not { } forum)
            return;

        var access = await AccessAsync(actor, ct);

        // The settings window builds each selector from the one before it (post messages from
        // read, start threads from post messages) and starts moderation at admins.
        if (
            !access.CanChangeSettings
            || !Enum.IsDefined((GuildForumPermission)readPermission)
            || !Enum.IsDefined((GuildForumPermission)postMessagePermission)
            || !Enum.IsDefined((GuildForumPermission)postThreadPermission)
            || !Enum.IsDefined((GuildForumPermission)moderatePermission)
            || postMessagePermission < readPermission
            || postThreadPermission < postMessagePermission
            || moderatePermission < (int)GuildForumPermission.Admins
        )
        {
            await DenyAsync(actor, "change the forum settings", ct);

            return;
        }

        forum.ReadPermission = (GuildForumPermission)readPermission;
        forum.PostMessagePermission = (GuildForumPermission)postMessagePermission;
        forum.PostThreadPermission = (GuildForumPermission)postThreadPermission;
        forum.ModeratePermission = (GuildForumPermission)moderatePermission;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            dbCtx.GuildForums.Update(forum);
            await dbCtx.SaveChangesAsync(ct);
            dbCtx.Entry(forum).State = EntityState.Detached;
        }

        await SendAsync(
            actor,
            new ForumDataMessageComposer { Forum = await DetailAsync(actor, access, ct) },
            ct
        );
        await NotifyAsync(actor, GuildForumNotificationTypes.SETTINGS_UPDATED, ct);
    }

    private async Task<GuildForumAccess> AccessAsync(PlayerId player, CancellationToken ct)
    {
        var rank = _grainFactory.GetGuildGrain(_state.GuildId).GetMemberRankAsync(player, ct);
        var staff = _grainFactory.HasPermissionAsync(player, PermissionNodes.Moderation.TOOL, ct);

        await Task.WhenAll(rank, staff);

        return new GuildForumAccess(rank.Result, staff.Result);
    }

    /// <summary>The viewer's access when they may read the forum; otherwise refused and null.</summary>
    private async Task<GuildForumAccess?> ReaderAsync(PlayerId viewer, CancellationToken ct)
    {
        if (_state.Forum is not { } forum)
            return null;

        var access = await AccessAsync(viewer, ct);

        if (access.Error(forum.ReadPermission) == GuildForumPermissionErrors.NONE)
            return access;

        await DenyAsync(viewer, "read the forum", ct);

        return null;
    }

    private async Task<GuildForumDetailSnapshot> DetailAsync(
        PlayerId viewer,
        GuildForumAccess access,
        CancellationToken ct
    )
    {
        var forum = _state.Forum!;
        var now = DateTime.UtcNow;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var row = (
            await GuildForumQueries.ForumsAsync(
                dbCtx,
                [_state.GuildId.Value],
                viewer.Value,
                now.AddDays(-_config.ForumActivityDays),
                now,
                ct
            )
        )[0];
        var read = access.Error(forum.ReadPermission);

        return new(row)
        {
            ReadPermission = forum.ReadPermission,
            PostMessagePermission = forum.PostMessagePermission,
            PostThreadPermission = forum.PostThreadPermission,
            ModeratePermission = forum.ModeratePermission,
            ReadError = read,
            PostMessageError = access.Error(forum.PostMessagePermission),
            PostThreadError = access.Error(forum.PostThreadPermission),
            ModerateError = access.Error(forum.ModeratePermission),
            // Whoever may read a post may report it (inference: no capture shows a refusal).
            ReportError = read,
            CanChangeSettings = access.CanChangeSettings,
            IsStaff = access.IsStaff,
        };
    }

    private Task<GuildForumThreadEntity?> FindThreadAsync(
        TurboDbContext dbCtx,
        int threadId,
        CancellationToken ct,
        bool tracked = false
    ) =>
        (
            tracked ? dbCtx.GuildForumThreads : dbCtx.GuildForumThreads.AsNoTracking()
        ).FirstOrDefaultAsync(x => x.Id == threadId && x.GuildEntityId == _state.GuildId.Value, ct);

    private async Task<ImmutableArray<GuildForumThreadSnapshot>> ThreadSnapshotsAsync(
        TurboDbContext dbCtx,
        List<GuildForumThreadEntity> threads,
        PlayerId viewer,
        GuildForumAccess access,
        CancellationToken ct
    )
    {
        var forum = _state.Forum!;
        var threadIds = threads.Select(x => x.Id).ToList();
        var marker = await dbCtx
            .GuildForumReadMarkers.AsNoTracking()
            .Where(x => x.PlayerEntityId == viewer.Value && x.GuildEntityId == _state.GuildId.Value)
            .Select(x => x.LastReadMessageId)
            .FirstOrDefaultAsync(ct);
        var lastMessages = await dbCtx
            .GuildForumMessages.AsNoTracking()
            .Where(x => threadIds.Contains(x.ThreadEntityId))
            .GroupBy(x => x.ThreadEntityId)
            .Select(x => x.OrderByDescending(m => m.ThreadIndex).First())
            .ToDictionaryAsync(x => x.ThreadEntityId, ct);
        var unread = await dbCtx
            .GuildForumMessages.AsNoTracking()
            .Where(x => threadIds.Contains(x.ThreadEntityId) && x.ForumMessageId > marker)
            .GroupBy(x => x.ThreadEntityId)
            .Select(x => new { ThreadId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.ThreadId, x => x.Count, ct);
        var names = await NamesAsync(
            dbCtx,
            threads
                .Select(x => x.PlayerEntityId)
                .Concat(threads.Select(x => x.ModeratorEntityId ?? 0))
                .Concat(lastMessages.Values.Select(x => x.PlayerEntityId)),
            ct
        );
        var now = DateTime.UtcNow;

        return
        [
            .. threads.Select(thread =>
            {
                var last = lastMessages.GetValueOrDefault(thread.Id);

                return thread.ToSnapshot(
                    access.MaySee(thread.State, forum.ModeratePermission) ? thread.Subject : "",
                    names.GetValueOrDefault(thread.PlayerEntityId, ""),
                    last,
                    last is null ? "" : names.GetValueOrDefault(last.PlayerEntityId, ""),
                    unread.GetValueOrDefault(thread.Id),
                    names.GetValueOrDefault(thread.ModeratorEntityId ?? 0, ""),
                    now
                );
            }),
        ];
    }

    private async Task<ImmutableArray<GuildForumMessageSnapshot>> MessageSnapshotsAsync(
        TurboDbContext dbCtx,
        List<GuildForumMessageEntity> messages,
        GuildForumAccess access,
        CancellationToken ct
    )
    {
        var forum = _state.Forum!;
        var authorIds = messages.Select(x => x.PlayerEntityId).Distinct().ToList();
        var authors = await dbCtx
            .Players.AsNoTracking()
            .Where(x => authorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        // An author's post count is their messages in this forum (inference: the client shows
        // it beside every post and nothing says over what).
        var posts = await dbCtx
            .GuildForumMessages.AsNoTracking()
            .Where(x =>
                x.GuildEntityId == _state.GuildId.Value && authorIds.Contains(x.PlayerEntityId)
            )
            .GroupBy(x => x.PlayerEntityId)
            .Select(x => new { PlayerId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.PlayerId, x => x.Count, ct);
        var moderators = await NamesAsync(
            dbCtx,
            messages.Select(x => x.ModeratorEntityId ?? 0),
            ct
        );
        var now = DateTime.UtcNow;

        return
        [
            .. messages.Select(x =>
                x.ToSnapshot(
                    access.MaySee(x.State, forum.ModeratePermission) ? x.Text : "",
                    authors.GetValueOrDefault(x.PlayerEntityId),
                    posts.GetValueOrDefault(x.PlayerEntityId),
                    moderators.GetValueOrDefault(x.ModeratorEntityId ?? 0, ""),
                    now
                )
            ),
        ];
    }

    private static Task<Dictionary<int, string>> NamesAsync(
        TurboDbContext dbCtx,
        IEnumerable<int> playerIds,
        CancellationToken ct
    )
    {
        var ids = playerIds.Where(x => x > 0).Distinct().ToList();

        return dbCtx
            .Players.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
    }

    private Task SendAsync(PlayerId player, IComposer composer, CancellationToken ct) =>
        _grainFactory.SendComposerToPlayerAsync(player, composer, ct);

    /// <summary>
    /// A refused request: logged, and the player told with forums.error.access_denied ("Seems like
    /// you do not have rights to perform requested action. Please refresh forum view.").
    /// </summary>
    private Task DenyAsync(PlayerId player, string action, CancellationToken ct)
    {
        _logger.LogWarning(
            "Player {PlayerId} may not {Action} in the forum of group {GuildId}",
            player,
            action,
            _state.GuildId
        );

        return NotifyAsync(player, GuildForumNotificationTypes.ACCESS_DENIED, ct);
    }

    private Task NotifyAsync(PlayerId player, string type, CancellationToken ct) =>
        NotifyAsync(
            player,
            type,
            ImmutableDictionary<string, string>.Empty.Add("display", "BUBBLE"),
            ct
        );

    private Task NotifyAsync(
        PlayerId player,
        string type,
        ImmutableDictionary<string, string> parameters,
        CancellationToken ct
    ) =>
        SendAsync(
            player,
            new NotificationDialogMessageComposer
            {
                NotificationType = type,
                Parameters = parameters,
            },
            ct
        );
}
