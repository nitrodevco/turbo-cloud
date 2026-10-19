using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Quests;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Quests.Enums;

namespace Turbo.Admin.Content;

/// <summary>
/// The daily tasks and the quests as staff keep them in the panel. A daily task definition is
/// checked as the hotel checks it before giving it (<see cref="DailyTaskDefinitionEntity.Problem"/>),
/// so one that would be skipped is refused with why, unless it is saved turned off; each change
/// has the players online read the definitions again, as their tasks keep them for the day.
/// Quests are read on every use, so a change reaches players at once. Taking a task or a quest
/// away takes the players' progress on it with it. Every change is logged with who made it.
/// </summary>
public sealed partial class AdminQuestEditor(
    IDbContextFactory<TurboDbContext> database,
    IGrainFactory grainFactory,
    ISessionGateway sessions,
    ILogger<AdminQuestEditor> logger
)
{
    public const string POINTS = "points";
    public const string BADGE = "badge";

    /// <summary>The quest types the server counts; any other never moves.</summary>
    public static readonly string[] COUNTED_TYPES =
    [
        QuestTypes.WEAR_BADGE,
        QuestTypes.PET_EAT,
        QuestTypes.FRIEND_REQUEST,
    ];

    public async Task<DailyTasksResponse> ListDailyTasksAsync(bool canManage, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var given = await db
            .PlayerDailyTasks.AsNoTracking()
            .GroupBy(x => x.DefinitionEntityId)
            .Select(x => new
            {
                Id = x.Key,
                Count = x.Select(y => y.PlayerEntityId).Distinct().Count(),
            })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct)
            .ConfigureAwait(false);
        var definitions = await db
            .DailyTaskDefinitions.AsNoTracking()
            .OrderBy(x => x.IsBonus)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new DailyTasksResponse(
            canManage,
            [DailyTaskTypes.EXPLORE, DailyTaskTypes.FIND_FURNI],
            [
                .. definitions.Select(x => new DailyTaskItem(
                    x.Id,
                    x.Code,
                    x.QuestType,
                    x.Target,
                    x.RequiredRepeats,
                    x.ImageVersion,
                    x.CatalogName,
                    x.IsBonus,
                    x.Enabled,
                    x.RewardProductType == ProductDisplayType.Badge ? BADGE : POINTS,
                    x.RewardTypeId,
                    x.RewardAmount,
                    x.Problem(),
                    given.GetValueOrDefault(x.Id)
                )),
            ]
        );
    }

    /// <summary>Saves a definition, a new one when <paramref name="id"/> is 0; its id.</summary>
    /// <exception cref="ArgumentException">It isn't one the hotel can give.</exception>
    public async Task<int> SaveDailyTaskAsync(
        int id,
        DailyTaskRequest request,
        string actor,
        CancellationToken ct
    )
    {
        var code = Code(request.Code, "A task's code");
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row =
            id == 0
                ? null
                : await db
                    .DailyTaskDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct)
                    .ConfigureAwait(false)
                    ?? throw new ArgumentException("There is no such task.");

        if (
            await db
                .DailyTaskDefinitions.AnyAsync(x => x.Code == code && x.Id != id, ct)
                .ConfigureAwait(false)
        )
            throw new ArgumentException($"Another task is {code} already.");

        row ??= db.DailyTaskDefinitions.Add(new() { Code = code, QuestType = "" }).Entity;
        row.Code = code;
        row.QuestType = (request.QuestType ?? string.Empty).Trim();
        row.Target = Text(request.Target, 512, "The furni to find");
        row.RequiredRepeats = request.RequiredRepeats;
        row.ImageVersion = Text(request.ImageVersion, 32, "The image version");
        row.CatalogName = Text(request.CatalogName, 64, "The catalog page");
        row.IsBonus = request.IsBonus;
        row.Enabled = request.Enabled;
        row.RewardProductType = request.RewardKind switch
        {
            BADGE => ProductDisplayType.Badge,
            POINTS => ProductDisplayType.ActivityPoints,
            _ => throw new ArgumentException("A task rewards points or a badge."),
        };
        row.RewardTypeId = Text(request.RewardTypeId, 64, "The reward");
        row.RewardAmount = Math.Max(0, request.RewardAmount);

        // One that would be skipped may be kept turned off, to finish later.
        if (row.Enabled && row.Problem() is { } problem)
            throw new ArgumentException(problem);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        DailyTasksChanged(actor, id == 0 ? "added" : "changed", row.Code);

        return row.Id;
    }

    /// <summary>Takes a definition away, and the players' tasks of it; false when there is none.</summary>
    public async Task<bool> RemoveDailyTaskAsync(int id, string actor, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row = await db
            .DailyTaskDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        db.DailyTaskDefinitions.Remove(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        DailyTasksChanged(actor, "removed", row.Code);

        return true;
    }

    public async Task<QuestsResponse> ListQuestsAsync(bool canManage, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var progress = await db
            .PlayerQuests.AsNoTracking()
            .GroupBy(x => x.QuestEntityId)
            .Select(x => new
            {
                Id = x.Key,
                Started = x.Count(),
                Completed = x.Count(y => y.CompletedAt != null),
            })
            .ToDictionaryAsync(x => x.Id, ct)
            .ConfigureAwait(false);
        var campaigns = await db
            .QuestCampaigns.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var quests = await db
            .Quests.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new QuestsResponse(
            canManage,
            COUNTED_TYPES,
            [
                .. campaigns.Select(campaign => new QuestCampaignItem(
                    campaign.Id,
                    campaign.Code,
                    campaign.SortOrder,
                    campaign.Enabled,
                    campaign.EndsAt,
                    [
                        .. quests
                            .Where(x => x.CampaignEntityId == campaign.Id)
                            .Select(x => new QuestItem(
                                x.Id,
                                x.CampaignEntityId,
                                x.LocalizationCode,
                                x.Type,
                                x.Target,
                                x.TotalSteps,
                                x.ActivityPointType,
                                x.RewardAmount,
                                x.SortOrder,
                                x.ImageVersion,
                                x.CatalogPageName,
                                x.ChainCode,
                                x.Easy,
                                COUNTED_TYPES.Contains(x.Type),
                                progress.GetValueOrDefault(x.Id)?.Started ?? 0,
                                progress.GetValueOrDefault(x.Id)?.Completed ?? 0
                            )),
                    ]
                )),
            ]
        );
    }

    public async Task<int> SaveCampaignAsync(
        int id,
        QuestCampaignRequest request,
        string actor,
        CancellationToken ct
    )
    {
        var code = Code(request.Code, "A campaign's code");
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row =
            id == 0
                ? null
                : await db
                    .QuestCampaigns.FirstOrDefaultAsync(x => x.Id == id, ct)
                    .ConfigureAwait(false)
                    ?? throw new ArgumentException("There is no such campaign.");

        if (
            await db
                .QuestCampaigns.AnyAsync(x => x.Code == code && x.Id != id, ct)
                .ConfigureAwait(false)
        )
            throw new ArgumentException($"Another campaign is {code} already.");

        row ??= db.QuestCampaigns.Add(new() { Code = code }).Entity;
        row.Code = code;
        row.SortOrder = request.SortOrder;
        row.Enabled = request.Enabled;
        row.EndsAt = request.EndsAtUtc is { } ends
            ? DateTime.SpecifyKind(ends, DateTimeKind.Utc)
            : null;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation(
            "Quest campaign {Code} {What} by {Actor}",
            row.Code,
            id == 0 ? "added" : "changed",
            actor
        );

        return row.Id;
    }

    /// <summary>Takes a campaign away, its quests and the players' progress on them; false when there is none.</summary>
    public async Task<bool> RemoveCampaignAsync(int id, string actor, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row = await db
            .QuestCampaigns.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        db.QuestCampaigns.Remove(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Quest campaign {Code} removed by {Actor}", row.Code, actor);

        return true;
    }

    public async Task<int> SaveQuestAsync(
        int id,
        QuestRequest request,
        string actor,
        CancellationToken ct
    )
    {
        var code = Code(request.LocalizationCode, "A quest's code");
        var type = (request.Type ?? string.Empty).Trim();

        if (type.Length is 0 or > QuestEntity.CODE_MAX_LENGTH)
            throw new ArgumentException("A quest needs a type: what counts towards it.");

        if (request.TotalSteps < 1)
            throw new ArgumentException("A quest needs at least one step.");

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        if (
            !await db
                .QuestCampaigns.AnyAsync(x => x.Id == request.CampaignId, ct)
                .ConfigureAwait(false)
        )
            throw new ArgumentException("There is no such campaign.");

        var row =
            id == 0
                ? null
                : await db.Quests.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false)
                    ?? throw new ArgumentException("There is no such quest.");

        if (
            await db
                .Quests.AnyAsync(
                    x =>
                        x.CampaignEntityId == request.CampaignId
                        && x.LocalizationCode == code
                        && x.Id != id,
                    ct
                )
                .ConfigureAwait(false)
        )
            throw new ArgumentException($"The campaign has a quest {code} already.");

        row ??= db
            .Quests.Add(
                new()
                {
                    CampaignEntityId = request.CampaignId,
                    LocalizationCode = code,
                    Type = type,
                }
            )
            .Entity;
        row.CampaignEntityId = request.CampaignId;
        row.LocalizationCode = code;
        row.Type = type;
        row.Target = Text(request.Target, QuestEntity.TARGET_MAX_LENGTH, "The target");
        row.TotalSteps = request.TotalSteps;
        row.ActivityPointType = Math.Max(0, request.ActivityPointType);
        row.RewardAmount = Math.Max(0, request.RewardAmount);
        row.SortOrder = request.SortOrder;
        row.ImageVersion = Text(
            request.ImageVersion,
            QuestEntity.IMAGE_VERSION_MAX_LENGTH,
            "The image version"
        );
        row.CatalogPageName = Text(
            request.CatalogPageName,
            QuestEntity.CODE_MAX_LENGTH,
            "The catalog page"
        );
        row.ChainCode = Text(request.ChainCode, QuestEntity.CODE_MAX_LENGTH, "The chain");
        row.Easy = request.Easy;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation(
            "Quest {Code} of campaign {Campaign} {What} by {Actor}",
            row.LocalizationCode,
            row.CampaignEntityId,
            id == 0 ? "added" : "changed",
            actor
        );

        return row.Id;
    }

    /// <summary>Takes a quest away, and the players' progress on it; false when there is none.</summary>
    public async Task<bool> RemoveQuestAsync(int id, string actor, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row = await db.Quests.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);

        if (row is null)
            return false;

        db.Quests.Remove(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Quest {Code} removed by {Actor}", row.LocalizationCode, actor);

        return true;
    }

    /// <summary>Logs the change and has the players online read the definitions again.</summary>
    private void DailyTasksChanged(string actor, string what, string code)
    {
        logger.LogInformation("Daily task {Code} {What} by {Actor}", code, what, actor);

        foreach (var playerId in sessions.GetOnlinePlayerIds())
            grainFactory
                .GetPlayerDailyTaskGrain(playerId)
                .ReloadAsync(CancellationToken.None)
                .LogAndForget(
                    logger,
                    "have player {PlayerId} read the daily tasks again",
                    playerId
                );
    }

    /// <summary>A code the client's texts are named by: letters, digits, _, - and ., 64 at most.</summary>
    private static string Code(string? given, string what)
    {
        var code = (given ?? string.Empty).Trim();

        if (!CodePattern().IsMatch(code))
            throw new ArgumentException(
                $"{what} is letters, digits, _, - and ., 64 at most: it names the client's texts."
            );

        return code;
    }

    private static string Text(string? given, int max, string what)
    {
        var text = (given ?? string.Empty).Trim();

        if (text.Length > max)
            throw new ArgumentException($"{what} is {max} characters at most.");

        return text;
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,64}$")]
    private static partial Regex CodePattern();
}
