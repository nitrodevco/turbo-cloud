using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Hands the selected users a prize. Params: how often one user may claim (once, or once per
/// N days / hours / minutes), unique mode (every prize at most once per user), the total prize
/// limit (zero: none) and N. The string param lists prizes as "type,code,probability" joined
/// by semicolons: type 0 is a badge code, type 1 a furniture definition name. Claims are kept
/// on the box so limits survive a room reload.
/// </summary>
[RoomObjectLogic("wf_act_give_reward")]
public class WiredActionGiveReward(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.GIVE_REWARD;

    /// <summary>
    /// Rewards are badges and furniture created from nothing, so a room owner must never be
    /// able to configure them: only hotel staff acting in the room may save this box.
    /// </summary>
    public override RoomControllerType MinimumControllerLevelToSave => RoomControllerType.Moderator;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredRewardIntervalType>(WiredRewardIntervalType.Once),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(0, 1000, 0),
            new WiredRangeParamRule(1, 1000, 1),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var rewards = ParseRewards(_wiredData.StringParam);

        if (rewards.Count == 0)
            return false;

        var players = GetPlayers(ctx.GetSelection(this));

        if (players.Count == 0)
            return false;

        var claims = LoadClaims();
        var interval = GetIntParamOrDefault(0, WiredRewardIntervalType.Once);
        var unique = GetIntParamOrDefault(1, false);
        var prizeLimit = GetIntParamOrDefault(2, 0);
        var intervalCount = GetIntParamOrDefault(3, 1);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var given = false;

        foreach (var player in players)
        {
            var totalClaims = claims.Values.Sum(x => x.Count);

            if (prizeLimit > 0 && totalClaims >= prizeLimit)
            {
                await NotifyAsync(player, WiredRewardResultType.LimitReached, ct);

                continue;
            }

            claims.TryGetValue(player.PlayerId, out var claim);
            claim ??= new WiredRewardClaim();

            if (!IsIntervalOpen(claim, interval, intervalCount, now))
            {
                await NotifyAsync(player, WiredRewardResultType.RewardAlreadyReceived, ct);

                continue;
            }

            var reward = PickReward(rewards, unique, claim);

            if (reward is null)
            {
                await NotifyAsync(
                    player,
                    unique
                        ? WiredRewardResultType.RewardAlreadyReceived
                        : WiredRewardResultType.ProbabilityMissed,
                    ct
                );

                continue;
            }

            var result = await GrantAsync(player, reward, ct);

            await NotifyAsync(player, result, ct);

            if (
                result
                is not (
                    WiredRewardResultType.RewardReceivedBadge
                    or WiredRewardResultType.RewardReceivedProduct
                )
            )
                continue;

            claim.Count++;
            claim.LastClaimUnix = now;
            claim.ReceivedCodes.Add(reward.Code);
            claims[player.PlayerId] = claim;
            given = true;
        }

        if (given)
            SaveClaims(claims);

        return given;
    }

    private static bool IsIntervalOpen(
        WiredRewardClaim claim,
        WiredRewardIntervalType interval,
        int count,
        long now
    )
    {
        if (claim.Count == 0)
            return true;

        var window = interval switch
        {
            WiredRewardIntervalType.PerDays => count * 86400L,
            WiredRewardIntervalType.PerHours => count * 3600L,
            WiredRewardIntervalType.PerMinutes => count * 60L,
            _ => long.MaxValue,
        };

        return window != long.MaxValue && now - claim.LastClaimUnix >= window;
    }

    private static WiredReward? PickReward(
        List<WiredReward> rewards,
        bool unique,
        WiredRewardClaim claim
    )
    {
        var candidates = unique
            ? rewards.Where(x => !claim.ReceivedCodes.Contains(x.Code)).ToList()
            : rewards;

        if (candidates.Count == 0)
            return null;

        if (unique)
            return candidates[Random.Shared.Next(candidates.Count)];

        var roll = Random.Shared.Next(1, 101);
        var cumulative = 0;

        foreach (var reward in candidates)
        {
            cumulative += reward.Probability;

            if (roll <= cumulative)
                return reward;
        }

        return null;
    }

    private async Task<WiredRewardResultType> GrantAsync(
        IRoomPlayer player,
        WiredReward reward,
        CancellationToken ct
    )
    {
        try
        {
            switch (reward.Type)
            {
                case WiredRewardType.Badge:
                    return await _grainFactory
                        .GetPlayerGrain(player.PlayerId)
                        .GiveBadgeAsync(reward.Code, ct)
                        ? WiredRewardResultType.RewardReceivedBadge
                        : WiredRewardResultType.RewardAlreadyReceived;
                case WiredRewardType.Product:
                {
                    var definition = _roomGrain._definitionProvider.TryGetDefinitionByName(
                        reward.Code
                    );

                    if (definition is null)
                        return WiredRewardResultType.RewardNotFound;

                    var granted = await _grainFactory
                        .GetInventoryGrain(player.PlayerId)
                        .GrantFurnitureAsync(definition.Id, null, ct);

                    return granted is null
                        ? WiredRewardResultType.RewardNotFound
                        : WiredRewardResultType.RewardReceivedProduct;
                }
                default:
                    return WiredRewardResultType.RewardNotFound;
            }
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogError(
                ex,
                "Wired reward {Code} could not be granted to player {PlayerId} in room {RoomId}",
                reward.Code,
                player.PlayerId,
                _roomGrain.RoomId
            );

            return WiredRewardResultType.RewardNotFound;
        }
    }

    private Task NotifyAsync(
        IRoomPlayer player,
        WiredRewardResultType result,
        CancellationToken ct
    ) =>
        _roomGrain._grainFactory.SendComposerToPlayerAsync(
            player.PlayerId,
            new WiredRewardResultMessageComposer { Reason = result },
            ct
        );

    private List<WiredReward> ParseRewards(string? stringParam)
    {
        var rewards = new List<WiredReward>();

        if (string.IsNullOrWhiteSpace(stringParam))
            return rewards;

        foreach (var entry in stringParam.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(',');

            if (parts.Length < 3)
                continue;

            if (
                !int.TryParse(parts[0], out var type)
                || !int.TryParse(parts[2], out var probability)
                || string.IsNullOrWhiteSpace(parts[1])
            )
                continue;

            rewards.Add(
                new WiredReward(
                    (WiredRewardType)type,
                    parts[1].Trim(),
                    Math.Clamp(probability, 0, 100)
                )
            );

            if (rewards.Count >= _roomGrain._roomConfig.WiredMaxRewardsPerBox)
                break;
        }

        return rewards;
    }

    private Dictionary<int, WiredRewardClaim> LoadClaims() =>
        FurnitureExtraDataSections.Read<Dictionary<int, WiredRewardClaim>>(
            _ctx.RoomObject.ExtraData,
            WiredRewardClaim.SECTION,
            _roomGrain._logger
        ) ?? [];

    private void SaveClaims(Dictionary<int, WiredRewardClaim> claims) =>
        _ctx.RoomObject.ExtraData.UpdateSection(WiredRewardClaim.SECTION, claims);
}
