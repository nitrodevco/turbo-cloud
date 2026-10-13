using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Moderation;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations;

/// <summary>
/// The call for help queue over <c>cfh_reports</c>: a report is open until a moderator picks it
/// (<c>picker_id</c>) and closed with a reason (<c>close_reason</c>). What the client reads but the
/// hotel does not decide - the source, priority and grouping - is derived from the report (inference,
/// no official recording: the official account is not staff):
/// <list type="bullet">
/// <item>source (<c>IssueCategoryNames.getSourceName</c>): 1 "Normal" for the help window, 6 "IM",
/// 11 "Discussion" for a forum post, 14 "Photo";</item>
/// <item>one priority for all, so the client orders by age (<c>IssueListView.update</c> sorts by
/// priority, then age);</item>
/// <item>grouping 1 for a report naming a player, 0 otherwise, so reports about the same player are
/// one bundle (<c>IssueBundle.matches</c>) and the rest stand alone.</item>
/// </list>
/// </summary>
public sealed class ModerationIssueService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    TimeProvider timeProvider,
    IOptions<OperationsConfig> config
) : IModerationIssueService
{
    public const int SOURCE_NORMAL = 1;
    public const int SOURCE_IM = 6;
    public const int SOURCE_DISCUSSION = 11;
    public const int SOURCE_PHOTO = 14;
    public const int PRIORITY = 1;

    /// <summary><c>ChatlogCtrl.populateEvidence</c>'s record types: 1 room, 2 IM, 3 forum thread, 4 forum message, 6 photo.</summary>
    private static int RecordType(CfhSourceType source) =>
        source switch
        {
            CfhSourceType.InstantMessage => 2,
            CfhSourceType.ForumThread => 3,
            CfhSourceType.ForumMessage => 4,
            CfhSourceType.Photo => 6,
            _ => ModChatlogRecordSnapshot.ROOM,
        };

    private const string TIME_FORMAT = "HH:mm";

    public async Task<ImmutableArray<ModIssueSnapshot>> GetOpenIssuesAsync(CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var reports = await dbCtx
            .CfhReports.AsNoTracking()
            .Where(x => x.ClosedAt == null)
            .OrderBy(x => x.Id)
            .Take(config.Value.ModerationIssuesListed)
            .ToListAsync(ct);

        return await SnapshotsAsync(dbCtx, reports, ct);
    }

    public async Task PublishAsync(int issueId, CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var report = await dbCtx
            .CfhReports.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == issueId, ct);

        if (report is null)
            return;

        await BroadcastAsync(await SnapshotsAsync(dbCtx, [report], ct), ct);
    }

    public async Task<ImmutableArray<ModIssuePickFailureSnapshot>> PickAsync(
        PlayerId moderator,
        ImmutableArray<int> issueIds,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var ids = issueIds.Distinct().ToList();
        var reports = await dbCtx.CfhReports.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        var failures = new List<(int IssueId, int PickerId)>();
        var picked = new List<CfhReportEntity>();

        foreach (var id in ids)
        {
            var report = reports.FirstOrDefault(x => x.Id == id);

            // Gone or closed: nobody holds it (IssueManager.issuePickFailed reads -1 as no picker).
            if (report is null || report.ClosedAt is not null)
            {
                failures.Add((id, -1));
                continue;
            }

            if (report.PickerEntityId is { } holder)
            {
                if (holder != moderator.Value)
                    failures.Add((id, holder));

                continue;
            }

            report.PickerEntityId = moderator.Value;
            picked.Add(report);
        }

        await dbCtx.SaveChangesAsync(ct);
        await BroadcastAsync(await SnapshotsAsync(dbCtx, picked, ct), ct);

        var names = await NamesAsync(dbCtx, failures.Select(x => x.PickerId), ct);

        return
        [
            .. failures.Select(x => new ModIssuePickFailureSnapshot
            {
                IssueId = x.IssueId,
                PickerUserId = x.PickerId,
                PickerUserName = names.GetValueOrDefault(x.PickerId, string.Empty),
            }),
        ];
    }

    public async Task ReleaseAsync(
        PlayerId moderator,
        ImmutableArray<int> issueIds,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var reports = await HeldAsync(dbCtx, moderator, issueIds, ct);

        foreach (var report in reports)
            report.PickerEntityId = null;

        await dbCtx.SaveChangesAsync(ct);
        await BroadcastAsync(await SnapshotsAsync(dbCtx, reports, ct), ct);
    }

    public async Task CloseAsync(
        PlayerId moderator,
        ImmutableArray<int> issueIds,
        CfhCloseReasonType reason,
        CancellationToken ct
    )
    {
        if (!Enum.IsDefined(reason))
            return;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var reports = await HeldAsync(dbCtx, moderator, issueIds, ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var report in reports)
        {
            report.ClosedAt = now;
            report.CloseReason = reason;
        }

        await dbCtx.SaveChangesAsync(ct);
        await BroadcastAsync(await SnapshotsAsync(dbCtx, reports, ct), ct);

        // An empty text is the client's own help.cfh.closed.<reason> (CallForHelpManager.onIssueClose).
        foreach (var reporter in reports.Select(x => x.ReporterEntityId).Distinct())
            await grainFactory.TrySendComposerToPlayerAsync(
                reporter,
                new IssueCloseNotificationMessageComposer
                {
                    CloseReason = reason,
                    MessageText = string.Empty,
                },
                ct
            );
    }

    public async Task<(
        int ReporterId,
        int ReportedId,
        ModChatlogRecordSnapshot Record
    )?> GetChatlogAsync(int issueId, CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var report = await dbCtx
            .CfhReports.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == issueId, ct);

        if (report is null)
            return null;

        var lines = await dbCtx
            .CfhReportChatLines.AsNoTracking()
            .Where(x => x.ReportEntityId == issueId)
            .OrderBy(x => x.Position)
            .ToListAsync(ct);
        var names = await NamesAsync(dbCtx, lines.Select(x => x.PlayerEntityId), ct);
        var roomName = report.RoomEntityId is { } roomId
            ? await dbCtx
                .Rooms.AsNoTracking()
                .Where(x => x.Id == roomId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct)
            : null;

        return (
            report.ReporterEntityId,
            report.ReportedEntityId ?? 0,
            new ModChatlogRecordSnapshot
            {
                RecordType = RecordType(report.Source),
                RoomId = report.RoomEntityId ?? 0,
                RoomName = roomName ?? string.Empty,
                Lines =
                [
                    .. lines.Select(x => new ModChatlogLineSnapshot
                    {
                        Time = x.CreatedAt.ToString(TIME_FORMAT, CultureInfo.InvariantCulture),
                        ChatterId = x.PlayerEntityId,
                        ChatterName = names.GetValueOrDefault(x.PlayerEntityId, string.Empty),
                        Message = x.Text,
                        Highlighted = x.PlayerEntityId == report.ReportedEntityId,
                    }),
                ],
            }
        );
    }

    public async Task<ModToolPreferencesSnapshot?> GetPreferencesAsync(
        PlayerId moderator,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        return await dbCtx
            .ModeratorToolPreferences.AsNoTracking()
            .Where(x => x.PlayerEntityId == moderator.Value)
            .Select(x => new ModToolPreferencesSnapshot
            {
                WindowX = x.WindowX,
                WindowY = x.WindowY,
                WindowWidth = x.WindowWidth,
                WindowHeight = x.WindowHeight,
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task SavePreferencesAsync(
        PlayerId moderator,
        ModToolPreferencesSnapshot preferences,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var row = await dbCtx.ModeratorToolPreferences.FirstOrDefaultAsync(
            x => x.PlayerEntityId == moderator.Value,
            ct
        );

        if (row is null)
        {
            row = new ModeratorToolPreferencesEntity { PlayerEntityId = moderator.Value };
            dbCtx.ModeratorToolPreferences.Add(row);
        }

        row.WindowX = preferences.WindowX;
        row.WindowY = preferences.WindowY;
        row.WindowWidth = preferences.WindowWidth;
        row.WindowHeight = preferences.WindowHeight;

        await dbCtx.SaveChangesAsync(ct);
    }

    /// <summary>The issues the moderator holds among <paramref name="issueIds"/>, open ones only.</summary>
    private static async Task<List<CfhReportEntity>> HeldAsync(
        TurboDbContext dbCtx,
        PlayerId moderator,
        ImmutableArray<int> issueIds,
        CancellationToken ct
    )
    {
        var ids = issueIds.ToList();

        return await dbCtx
            .CfhReports.Where(x =>
                ids.Contains(x.Id) && x.ClosedAt == null && x.PickerEntityId == moderator.Value
            )
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
    }

    private async Task BroadcastAsync(ImmutableArray<ModIssueSnapshot> issues, CancellationToken ct)
    {
        if (issues.IsEmpty)
            return;

        var moderators = await ModeratorsOnlineAsync(ct);

        foreach (var issue in issues)
            await grainFactory.SendComposerToPlayersAsync(
                moderators,
                new IssueInfoMessageComposer { Issue = issue },
                ct
            );
    }

    private async Task<List<PlayerId>> ModeratorsOnlineAsync(CancellationToken ct)
    {
        var moderators = new List<PlayerId>();

        foreach (var player in sessionGateway.GetOnlinePlayerIds())
            if (await grainFactory.HasPermissionAsync(player, PermissionNodes.Moderation.TOOL, ct))
                moderators.Add(player);

        return moderators;
    }

    private async Task<ImmutableArray<ModIssueSnapshot>> SnapshotsAsync(
        TurboDbContext dbCtx,
        IReadOnlyCollection<CfhReportEntity> reports,
        CancellationToken ct
    )
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var names = await NamesAsync(
            dbCtx,
            reports.SelectMany(x =>
                new[] { x.ReporterEntityId, x.ReportedEntityId ?? 0, x.PickerEntityId ?? 0 }
            ),
            ct
        );

        return
        [
            .. reports.Select(x => new ModIssueSnapshot
            {
                IssueId = x.Id,
                State =
                    x.ClosedAt is not null ? ModIssueSnapshot.STATE_CLOSED
                    : x.PickerEntityId is not null ? ModIssueSnapshot.STATE_PICKED
                    : ModIssueSnapshot.STATE_OPEN,
                CategoryId = x.Source switch
                {
                    CfhSourceType.InstantMessage => SOURCE_IM,
                    CfhSourceType.ForumThread or CfhSourceType.ForumMessage => SOURCE_DISCUSSION,
                    CfhSourceType.Photo => SOURCE_PHOTO,
                    _ => SOURCE_NORMAL,
                },
                ReportedCategoryId = x.TopicId,
                IssueAgeInMilliseconds = (int)
                    Math.Clamp((now - x.CreatedAt).TotalMilliseconds, 0, int.MaxValue),
                Priority = PRIORITY,
                GroupingId = x.ReportedEntityId is not null ? 1 : 0,
                ReporterUserId = x.ReporterEntityId,
                ReporterUserName = names.GetValueOrDefault(x.ReporterEntityId, string.Empty),
                ReportedUserId = x.ReportedEntityId ?? 0,
                ReportedUserName = names.GetValueOrDefault(x.ReportedEntityId ?? 0, string.Empty),
                PickerUserId = x.PickerEntityId ?? 0,
                PickerUserName = names.GetValueOrDefault(x.PickerEntityId ?? 0, string.Empty),
                Message = x.Message,
                ChatRecordId = x.Id,
            }),
        ];
    }

    private static async Task<Dictionary<int, string>> NamesAsync(
        TurboDbContext dbCtx,
        IEnumerable<int> playerIds,
        CancellationToken ct
    )
    {
        var ids = playerIds.Where(x => x > 0).Distinct().ToList();

        return await dbCtx
            .Players.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
    }
}
