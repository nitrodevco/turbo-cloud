using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Extensions;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Texts;

namespace Turbo.Operations;

/// <summary>
/// Calls for help. The topics are <c>cfh_topics</c>, read on first use and held: every login
/// sends them, and a hotel changes them about never (a change shows after a restart). Reports
/// are <c>cfh_reports</c> with their chat lines; staff close them (no screen for that yet), and
/// a reporter may appeal a report closed without action.
/// </summary>
public sealed class CallForHelpService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<OperationsConfig> config,
    IHotelTextProvider texts,
    TimeProvider timeProvider
) : ICallForHelpService
{
    public const string TOO_MANY_TEXT = "moderation.cfh.too_many";
    public const string UNKNOWN_TOPIC_TEXT = "moderation.cfh.unknown_topic";

    private const string DEFAULT_TOO_MANY_TEXT =
        "You already have %0% reports waiting for a moderator. Please wait until they are handled.";
    private const string DEFAULT_UNKNOWN_TOPIC_TEXT =
        "Choose what your report is about, then send it again.";

    private readonly OperationsConfig _config = config.Value;
    private readonly SemaphoreSlim _topicsLock = new(1, 1);
    private ImmutableArray<CfhCategorySnapshot>? _topics;

    public async Task<ImmutableArray<CfhCategorySnapshot>> GetTopicsAsync(CancellationToken ct)
    {
        if (_topics is { } held)
            return held;

        await _topicsLock.WaitAsync(ct);

        try
        {
            if (_topics is { } read)
                return read;

            await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

            var rows = await dbCtx
                .CfhTopics.AsNoTracking()
                .Where(x => x.Enabled)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);

            // GroupBy keeps the order each category is first met in.
            var topics = rows.GroupBy(x => x.Category)
                .Select(g => new CfhCategorySnapshot
                {
                    Name = g.Key,
                    Topics = [.. g.Select(x => x.ToSnapshot())],
                })
                .ToImmutableArray();

            _topics = topics;

            return topics;
        }
        finally
        {
            _topicsLock.Release();
        }
    }

    public async Task<CfhResultSnapshot> SubmitAsync(
        PlayerId reporter,
        CfhSubmissionSnapshot submission,
        CancellationToken ct
    )
    {
        var topics = await GetTopicsAsync(ct);

        if (!topics.Any(x => x.Topics.Any(t => t.Id == submission.TopicId)))
            return await RefusedAsync(UNKNOWN_TOPIC_TEXT, DEFAULT_UNKNOWN_TOPIC_TEXT, ct);

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var open = await dbCtx.CfhReports.CountAsync(
            x => x.ReporterEntityId == reporter.Value && x.ClosedAt == null,
            ct
        );

        if (open >= _config.CfhMaxOpenReports)
            return await RefusedAsync(
                TOO_MANY_TEXT,
                DEFAULT_TOO_MANY_TEXT,
                ct,
                open.ToString(CultureInfo.InvariantCulture)
            );

        dbCtx.CfhReports.Add(
            new CfhReportEntity
            {
                ReporterEntityId = reporter.Value,
                Source = submission.Source,
                ExtraDataId = OrNull(
                    submission.ExtraDataId,
                    CfhReportEntity.EXTRA_DATA_ID_MAX_LENGTH
                ),
                ItemEntityId = submission.ItemId > 0 ? submission.ItemId : null,
                ReportedEntityId =
                    submission.ReportedPlayerId > 0 ? submission.ReportedPlayerId : null,
                RoomEntityId = submission.RoomId > 0 ? submission.RoomId : null,
                TopicId = submission.TopicId,
                Message = ClientText.Truncate(
                    submission.Message,
                    CfhReportEntity.MESSAGE_MAX_LENGTH
                ),
                ReporterName = OrNull(submission.Name, CfhReportEntity.NAME_MAX_LENGTH),
                ReporterEmail = OrNull(submission.Email, CfhReportEntity.EMAIL_MAX_LENGTH),
                ChatLines =
                [
                    .. submission
                        .ChatLines.Take(_config.CfhMaxChatLines)
                        .Select(
                            (line, i) =>
                                new CfhReportChatLineEntity
                                {
                                    ReportEntityId = 0,
                                    Position = i,
                                    PlayerEntityId = line.PlayerId,
                                    Text = ClientText.Truncate(
                                        line.Text,
                                        CfhReportChatLineEntity.TEXT_MAX_LENGTH
                                    ),
                                }
                        ),
                ],
            }
        );

        await dbCtx.SaveChangesAsync(ct);

        // An empty text is the client's own "sent" text.
        return new CfhResultSnapshot { Result = CfhResultType.Sent, Message = string.Empty };
    }

    public async Task<ImmutableArray<CfhReportStatusSnapshot>> GetReportsAsync(
        PlayerId reporter,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var reports = await dbCtx
            .CfhReports.AsNoTracking()
            .Where(x => x.ReporterEntityId == reporter.Value)
            .OrderByDescending(x => x.Id)
            .Take(_config.CfhReportsListed)
            .ToListAsync(ct);
        var reportedIds = reports
            .Where(x => x.ReportedEntityId is not null)
            .Select(x => x.ReportedEntityId!.Value)
            .Distinct()
            .ToList();
        var names = await dbCtx
            .Players.AsNoTracking()
            .Where(x => reportedIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        return
        [
            .. reports.Select(x =>
                x.ToStatusSnapshot(
                    x.ReportedEntityId is { } id
                        ? names.TryGetValue(id, out var name)
                            ? name
                            : string.Empty
                        : string.Empty
                )
            ),
        ];
    }

    public async Task<bool> AppealAsync(PlayerId reporter, int reportId, CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var report = await dbCtx.CfhReports.FirstOrDefaultAsync(
            x => x.Id == reportId && x.ReporterEntityId == reporter.Value,
            ct
        );

        // MyReportStatus offers the button only for a report closed without action and not
        // appealed yet.
        if (
            report is null
            || report.ClosedAt is null
            || report.Sanctioned
            || report.AppealStatus != CfhAppealStatusType.None
        )
            return false;

        report.AppealStatus = CfhAppealStatusType.Appealed;
        report.AppealCreatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await dbCtx.SaveChangesAsync(ct);

        return true;
    }

    private async Task<CfhResultSnapshot> RefusedAsync(
        string key,
        string fallback,
        CancellationToken ct,
        params string[] parameters
    )
    {
        var text = await texts.GetTextAsync(key, ct) ?? fallback;

        for (var i = 0; i < parameters.Length; i++)
            text = text.Replace($"%{i}%", parameters[i], StringComparison.Ordinal);

        return new CfhResultSnapshot { Result = CfhResultType.Refused, Message = text };
    }

    private static string? OrNull(string value, int length) =>
        string.IsNullOrWhiteSpace(value) ? null : ClientText.Truncate(value.Trim(), length);
}
