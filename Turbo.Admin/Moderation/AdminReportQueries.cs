using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Texts;

namespace Turbo.Admin.Moderation;

/// <summary>
/// The calls for help as staff read them in the panel: newest first, open, closed or all,
/// narrowed to a player (who sent it or who it is about), paged by id as the chat log is; and
/// one with its chat lines, both players' histories and the other open reports about the same
/// player. Read-only: picking and closing go through <see cref="IModerationIssueService"/>, as
/// the client's mod tool does, so moderators in the hotel see it.
/// </summary>
public sealed class AdminReportQueries(
    IDbContextFactory<TurboDbContext> database,
    IHotelTextProvider texts,
    IModerationIssueService issues,
    IOptions<AdminConfig> config
)
{
    public const string OPEN = "open";
    public const string PICKED = "picked";
    public const string CLOSED = "closed";
    public const string ALL = "all";

    private const string TOPIC_TEXT = "help.cfh.topic.";
    private const string CATEGORY_TEXT = "help.cfh.reason.";

    /// <summary>
    /// A run of reports in <paramref name="state"/> (open, which takes in picked, closed or all).
    /// <paramref name="before"/> asks for those older than that one, <paramref name="after"/>
    /// for those newer; with neither, the newest.
    /// </summary>
    public async Task<ReportListResponse> ListAsync(
        string? state,
        string? player,
        int? before,
        int? after,
        CancellationToken ct
    )
    {
        var pageSize = Math.Max(1, config.Value.ReportsPageSize);
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var open = await db
            .CfhReports.AsNoTracking()
            .Where(x => x.ClosedAt == null)
            .GroupBy(x => x.PickerEntityId != null)
            .Select(x => new { Picked = x.Key, Count = x.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var pickedCount = open.Where(x => x.Picked).Sum(x => x.Count);
        var openCount = open.Sum(x => x.Count);

        var reports = db.CfhReports.AsNoTracking();

        reports = (state ?? OPEN) switch
        {
            CLOSED => reports.Where(x => x.ClosedAt != null),
            ALL => reports,
            _ => reports.Where(x => x.ClosedAt == null),
        };

        var who = (player ?? string.Empty).Trim();

        if (who.Length > 0)
        {
            if (await PlayerIdAsync(db, who, ct).ConfigureAwait(false) is not { } found)
                return new ReportListResponse(pageSize, false, false, openCount, pickedCount, []);

            reports = reports.Where(x =>
                x.ReporterEntityId == found || x.ReportedEntityId == found
            );
        }

        List<CfhReportEntity> rows;
        bool hasOlder;
        bool hasNewer;

        if (after is { } newerThan)
        {
            rows = await reports
                .Where(x => x.Id > newerThan)
                .OrderBy(x => x.Id)
                .Take(pageSize + 1)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            hasNewer = rows.Count > pageSize;
            rows = [.. rows.Take(pageSize).Reverse()];
            hasOlder = await reports.AnyAsync(x => x.Id <= newerThan, ct).ConfigureAwait(false);
        }
        else
        {
            var page = before is { } olderThan ? reports.Where(x => x.Id < olderThan) : reports;

            rows = await page.OrderByDescending(x => x.Id)
                .Take(pageSize + 1)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            hasOlder = rows.Count > pageSize;
            rows = [.. rows.Take(pageSize)];
            hasNewer =
                before is { } newest
                && await reports.AnyAsync(x => x.Id >= newest, ct).ConfigureAwait(false);
        }

        return new ReportListResponse(
            pageSize,
            hasOlder,
            hasNewer,
            openCount,
            pickedCount,
            await ItemsAsync(db, rows, ct).ConfigureAwait(false)
        );
    }

    /// <summary>One report with what came with it; null when there is no such report.</summary>
    public async Task<ReportDetailResponse?> GetAsync(int id, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var report = await db
            .CfhReports.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (report is null)
            return null;

        var lines = await db
            .CfhReportChatLines.AsNoTracking()
            .Where(x => x.ReportEntityId == id)
            .OrderBy(x => x.Position)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var related = report.ReportedEntityId is { } reported
            ? await db
                .CfhReports.AsNoTracking()
                .Where(x => x.ReportedEntityId == reported && x.ClosedAt == null && x.Id != id)
                .OrderByDescending(x => x.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false)
            : [];
        var items = await ItemsAsync(db, [report, .. related], ct).ConfigureAwait(false);
        var names = await NamesAsync(db, lines.Select(x => x.PlayerEntityId), ct)
            .ConfigureAwait(false);
        var groupName = report.GuildEntityId is { } groupId
            ? await db
                .Guilds.AsNoTracking()
                .Where(x => x.Id == groupId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false)
            : null;
        var aboutReported = report.ReportedEntityId is { } about
            ? await HistoryAsync(db.CfhReports.Where(x => x.ReportedEntityId == about), ct)
                .ConfigureAwait(false)
            : null;
        var bySender = await HistoryAsync(
                db.CfhReports.Where(x => x.ReporterEntityId == report.ReporterEntityId),
                ct
            )
            .ConfigureAwait(false);
        var sanction = report.ReportedEntityId is { } next
            ? await issues.GetDefaultSanctionAsync(new PlayerId(next), ct).ConfigureAwait(false)
            : null;

        return new ReportDetailResponse(
            items[0],
            [
                .. lines.Select(x => new ReportChatLine(
                    x.CreatedAt,
                    x.PlayerEntityId,
                    names.GetValueOrDefault(x.PlayerEntityId),
                    x.Text,
                    x.PlayerEntityId == report.ReportedEntityId
                )),
            ],
            report.ReporterName,
            report.ReporterEmail,
            report.ExtraDataId,
            report.ItemEntityId,
            report.GuildEntityId,
            groupName,
            report.ForumThreadEntityId,
            report.ForumMessageId,
            report.AppealStatus switch
            {
                CfhAppealStatusType.Appealed => "appealed",
                CfhAppealStatusType.DecidedWithAction => "upheld",
                CfhAppealStatusType.DecidedWithoutAction => "dismissed",
                _ => "none",
            },
            aboutReported,
            bySender,
            sanction is null ? null : new ReportSanction(sanction.Name, sanction.LengthHours),
            items[1..]
        );
    }

    /// <summary>A player by id, or by name.</summary>
    private static async Task<int?> PlayerIdAsync(
        TurboDbContext db,
        string who,
        CancellationToken ct
    ) =>
        int.TryParse(who, out var id)
            ? id
            : await db
                .Players.AsNoTracking()
                .Where(x => x.NameKey == PlayerNames.Key(who))
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

    private static async Task<ReportHistory> HistoryAsync(
        IQueryable<CfhReportEntity> reports,
        CancellationToken ct
    )
    {
        var counts = await reports
            .AsNoTracking()
            .GroupBy(x => new { x.Sanctioned, x.CloseReason })
            .Select(x => new
            {
                x.Key.Sanctioned,
                x.Key.CloseReason,
                Count = x.Count(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new ReportHistory(
            counts.Sum(x => x.Count),
            counts.Where(x => x.Sanctioned).Sum(x => x.Count),
            counts.Where(x => x.CloseReason == CfhCloseReasonType.Abusive).Sum(x => x.Count),
            counts.Where(x => x.CloseReason == CfhCloseReasonType.Useless).Sum(x => x.Count)
        );
    }

    /// <summary>The reports with their players', rooms' and topics' names, in one query each.</summary>
    private async Task<ReportItem[]> ItemsAsync(
        TurboDbContext db,
        List<CfhReportEntity> rows,
        CancellationToken ct
    )
    {
        var names = await NamesAsync(
                db,
                rows.SelectMany(x =>
                    new[] { x.ReporterEntityId, x.ReportedEntityId ?? 0, x.PickerEntityId ?? 0 }
                ),
                ct
            )
            .ConfigureAwait(false);
        var roomIds = rows.Select(x => x.RoomEntityId).OfType<int>().Distinct().ToList();
        var rooms = await db
            .Rooms.AsNoTracking()
            .Where(x => roomIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);
        var topicIds = rows.Select(x => x.TopicId).Distinct().ToList();
        var topics = await db
            .CfhTopics.AsNoTracking()
            .Where(x => topicIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct)
            .ConfigureAwait(false);
        var topicTexts = await texts
            .GetTextsAsync(
                [
                    .. topicIds.Select(x => $"{TOPIC_TEXT}{x}"),
                    .. topics.Values.Select(x => $"{CATEGORY_TEXT}{x.Category}").Distinct(),
                ],
                ct
            )
            .ConfigureAwait(false);

        string Topic(int topicId) =>
            topicTexts.TryGetText($"{TOPIC_TEXT}{topicId}", out var text) ? text
            : topics.TryGetValue(topicId, out var topic) ? topic.Name
            : $"topic #{topicId}";

        string Category(int topicId) =>
            !topics.TryGetValue(topicId, out var topic) ? string.Empty
            : topicTexts.TryGetText($"{CATEGORY_TEXT}{topic.Category}", out var text) ? text
            : topic.Category;

        return
        [
            .. rows.Select(x => new ReportItem(
                x.Id,
                x.CreatedAt,
                x.ClosedAt is not null ? CLOSED
                    : x.PickerEntityId is not null ? PICKED
                    : OPEN,
                x.Source switch
                {
                    CfhSourceType.InstantMessage => "im",
                    CfhSourceType.Photo => "photo",
                    CfhSourceType.ForumThread or CfhSourceType.ForumMessage => "forum",
                    _ => "room",
                },
                x.TopicId,
                Topic(x.TopicId),
                Category(x.TopicId),
                x.ReporterEntityId,
                names.GetValueOrDefault(x.ReporterEntityId),
                x.ReportedEntityId,
                x.ReportedEntityId is { } reported ? names.GetValueOrDefault(reported) : null,
                x.RoomEntityId,
                x.RoomEntityId is { } room ? rooms.GetValueOrDefault(room) : null,
                x.PickerEntityId,
                x.PickerEntityId is { } picker ? names.GetValueOrDefault(picker) : null,
                x.Message,
                x.ClosedAt,
                x.CloseReason switch
                {
                    CfhCloseReasonType.Useless => "useless",
                    CfhCloseReasonType.Abusive => "abusive",
                    CfhCloseReasonType.Resolved => "resolved",
                    _ => null,
                },
                x.Sanctioned
            )),
        ];
    }

    private static async Task<Dictionary<int, string>> NamesAsync(
        TurboDbContext db,
        IEnumerable<int> playerIds,
        CancellationToken ct
    )
    {
        var ids = playerIds.Where(x => x > 0).Distinct().ToList();

        return await db
            .Players.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);
    }
}
