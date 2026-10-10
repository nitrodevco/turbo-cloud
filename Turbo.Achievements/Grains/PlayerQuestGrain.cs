using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Quests;
using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Quests.Grains;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Achievements.Grains;

/// <summary>
/// A player's quests, write-through and read from the database on every call (the window is
/// opened rarely and a quest step is a single row). A campaign's current quest is its first one
/// the player has not completed; a player does one quest at a time, as the AS3 tracker shows one
/// (accepting another drops the first). A campaign done is sent with quest id 0, which the
/// client's <c>QuestMessageData.completedCampaign</c> reads as done.
/// </summary>
internal sealed class PlayerQuestGrain : Grain, IPlayerQuestGrain
{
    private const string CREDIT_REFERENCE_PREFIX = "quest:";

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly IGrainFactory _grainFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<IPlayerQuestGrain> _logger;
    private readonly PlayerId _playerId;

    public PlayerQuestGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IGrainFactory grainFactory,
        TimeProvider time,
        ILogger<IPlayerQuestGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _grainFactory = grainFactory;
        _time = time;
        _logger = logger;
        _playerId = this.GetPlayerId();
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task SendQuestsAsync(bool openWindow, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);
        var data = await LoadAsync(dbCtx, ct);
        var now = Now;

        await _grainFactory.SendComposerToPlayerAsync(
            _playerId,
            new QuestsMessageComposer
            {
                Quests =
                [
                    .. data
                        .Campaigns.Select(campaign => data.Current(campaign))
                        .Where(x => x is not null)
                        .Select(x => data.Snapshot(x!, now)),
                ],
                OpenWindow = openWindow,
            },
            ct
        );
    }

    public async Task AcceptAsync(int questId, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);
        var data = await LoadAsync(dbCtx, ct);
        var quest = data.Campaigns.Select(data.Current).FirstOrDefault(x => x?.Id == questId);

        // Only a campaign's current quest can be taken: one done, a later one, or another
        // player's id is refused without an answer.
        if (quest is null)
        {
            _logger.LogWarning(
                "Refused quest {QuestId} for player {PlayerId}: not a current quest",
                questId,
                _playerId
            );

            return;
        }

        foreach (var row in data.Rows.Values)
            row.Accepted = false;

        data.Row(dbCtx, quest).Accepted = true;
        await dbCtx.SaveChangesAsync(ct);

        await _grainFactory.SendComposerToPlayerAsync(
            _playerId,
            new QuestMessageComposer { Quest = data.Snapshot(quest, Now) },
            ct
        );
    }

    public async Task RejectAsync(int questId, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);
        var data = await LoadAsync(dbCtx, ct);
        var row = data.Rows.Values.FirstOrDefault(x =>
            x.Accepted && (questId == 0 || x.QuestEntityId == questId)
        );

        if (row is null || !data.Quests.TryGetValue(row.QuestEntityId, out var quest))
            return;

        row.Accepted = false;
        await dbCtx.SaveChangesAsync(ct);

        await _grainFactory.SendComposerToPlayerAsync(
            _playerId,
            new QuestCancelledMessageComposer
            {
                Expired = false,
                Quest = data.Snapshot(quest, Now),
            },
            ct
        );
    }

    public async Task RecordAsync(string type, string value, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);
        var data = await LoadAsync(dbCtx, ct);
        var row = data.Rows.Values.FirstOrDefault(x => x.Accepted);

        if (
            row is null
            || !data.Quests.TryGetValue(row.QuestEntityId, out var quest)
            || !string.Equals(quest.Type, type, StringComparison.Ordinal)
            || (
                quest.Target.Length > 0
                && !string.Equals(quest.Target, value, StringComparison.OrdinalIgnoreCase)
            )
        )
            return;

        var now = Now;
        row.Progress = Math.Min(row.Progress + 1, quest.TotalSteps);

        if (row.Progress < quest.TotalSteps)
        {
            await dbCtx.SaveChangesAsync(ct);
            await _grainFactory.SendComposerToPlayerAsync(
                _playerId,
                new QuestMessageComposer { Quest = data.Snapshot(quest, now) },
                ct
            );

            return;
        }

        // Paid before it is marked done, as a daily task is: the credit's reference makes a retry
        // pay once, and a quest saved as done before a credit that failed was never paid at all.
        // A credit refused or thrown leaves it on its last step, to be paid on the next one.
        if (quest.RewardAmount > 0)
        {
            var credit = await _grainFactory
                .GetPlayerWalletGrain(_playerId)
                .CreditAsync(
                    CurrencyKind.ActivityPoints(quest.ActivityPointType),
                    quest.RewardAmount,
                    $"{CREDIT_REFERENCE_PREFIX}{quest.Id}",
                    ct
                );

            if (credit == WalletCreditResult.Rejected)
            {
                _logger.LogError(
                    "Quest {QuestId} of player {PlayerId} could not pay {Amount} of activity point type {Type}; it stays on its last step",
                    quest.Id,
                    _playerId,
                    quest.RewardAmount,
                    quest.ActivityPointType
                );

                return;
            }
        }

        row.Accepted = false;
        row.CompletedAt = now;
        await dbCtx.SaveChangesAsync(ct);

        await _grainFactory.SendComposerToPlayerAsync(
            _playerId,
            new QuestCompletedMessageComposer
            {
                Quest = data.Snapshot(quest, now, completing: true),
                ShowDialog = true,
            },
            ct
        );
    }

    private async Task<QuestData> LoadAsync(TurboDbContext dbCtx, CancellationToken ct)
    {
        var now = Now;
        var campaigns = await dbCtx
            .QuestCampaigns.AsNoTracking()
            .Where(x => x.Enabled && (x.EndsAt == null || x.EndsAt > now))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
        var campaignIds = campaigns.Select(x => x.Id).ToList();
        var quests = await dbCtx
            .Quests.AsNoTracking()
            .Where(x => campaignIds.Contains(x.CampaignEntityId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
        var rows = await dbCtx
            .PlayerQuests.Where(x => x.PlayerEntityId == _playerId.Value)
            .ToDictionaryAsync(x => x.QuestEntityId, ct);

        return new QuestData(_playerId, campaigns, quests, rows);
    }

    private sealed class QuestData(
        PlayerId playerId,
        List<QuestCampaignEntity> campaigns,
        List<QuestEntity> quests,
        Dictionary<int, PlayerQuestEntity> rows
    )
    {
        public List<QuestCampaignEntity> Campaigns { get; } = campaigns;

        public Dictionary<int, QuestEntity> Quests { get; } = quests.ToDictionary(x => x.Id);

        public Dictionary<int, PlayerQuestEntity> Rows { get; } = rows;

        private readonly ILookup<int, QuestEntity> _byCampaign = quests.ToLookup(x =>
            x.CampaignEntityId
        );

        private bool Done(QuestEntity quest) =>
            Rows.TryGetValue(quest.Id, out var row) && row.CompletedAt is not null;

        /// <summary>The campaign's first quest not done, else its last (the campaign is done).</summary>
        public QuestEntity? Current(QuestCampaignEntity campaign) =>
            _byCampaign[campaign.Id].FirstOrDefault(x => !Done(x))
            ?? _byCampaign[campaign.Id].LastOrDefault();

        public PlayerQuestEntity Row(TurboDbContext dbCtx, QuestEntity quest)
        {
            if (Rows.TryGetValue(quest.Id, out var row))
                return row;

            row = new PlayerQuestEntity
            {
                PlayerEntityId = playerId.Value,
                QuestEntityId = quest.Id,
            };
            dbCtx.PlayerQuests.Add(row);
            Rows[quest.Id] = row;

            return row;
        }

        public QuestSnapshot Snapshot(QuestEntity quest, DateTime now, bool completing = false)
        {
            var campaign = Campaigns.First(x => x.Id == quest.CampaignEntityId);
            var inCampaign = _byCampaign[campaign.Id].ToList();
            var row = Rows.GetValueOrDefault(quest.Id);
            var completed = inCampaign.Count(Done);
            var campaignDone = !completing && completed == inCampaign.Count;

            return new QuestSnapshot
            {
                CampaignCode = campaign.Code,
                CompletedQuestsInCampaign = completed,
                QuestCountInCampaign = inCampaign.Count,
                ActivityPointType = quest.ActivityPointType,
                Id = campaignDone ? 0 : quest.Id,
                Accepted = row?.Accepted ?? false,
                Type = quest.Type,
                ImageVersion = quest.ImageVersion,
                RewardCurrencyAmount = quest.RewardAmount,
                LocalizationCode = quest.LocalizationCode,
                CompletedSteps = row?.Progress ?? 0,
                TotalSteps = quest.TotalSteps,
                SortOrder = campaign.SortOrder,
                CatalogPageName = quest.CatalogPageName,
                ChainCode = quest.ChainCode,
                Easy = quest.Easy,
                IsSeasonal = campaign.EndsAt is not null,
                SecondsLeft = campaign.EndsAt is { } ends
                    ? (int)Math.Max(0, (ends - now).TotalSeconds)
                    : 0,
            };
        }
    }
}
