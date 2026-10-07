using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Rooms.Wired;

/// <summary>
/// How a player's offer stands against a contract, the way the client's trade window reads it
/// (<c>WiredTradeRequirementsModel</c>): the options are alternatives, the elements of one
/// option are all needed, credits are paid in credit furni, and a furni element counts the
/// offered furni of its type.
/// </summary>
public static class WiredContractOffers
{
    /// <summary>The credits an offer's credit furni are worth.</summary>
    public static int Credits(IEnumerable<FurnitureItemSnapshot> offer) =>
        offer.Sum(item =>
            CreditFurniValue.TryParse(item.Definition.Name, out var value) ? value : 0
        );

    /// <summary>
    /// Whether the player may put this item in: something tradeable that sits in no room, and
    /// for a contract with requirements, credit furni when some option asks for credits and
    /// furni of a type some option asks for.
    /// </summary>
    public static bool CanOffer(WiredContractTradeRequest request, FurnitureItemSnapshot item)
    {
        if (!item.Definition.CanTrade || item.RoomId != 0)
            return false;

        if (request.IsDonation)
            return true;

        var nodes = GiveRules(request.Contract).SelectMany(x => x.Nodes).ToList();

        if (CreditFurniValue.TryParse(item.Definition.Name, out _))
            return nodes.Any(x => x.Type == TradeRequirementNodeType.Coin);

        var type = ChestItemTypes.Of(item.Definition, item.StuffData);

        return nodes.Any(x => x.Type == TradeRequirementNodeType.Furni && x.ItemType == type);
    }

    /// <summary>
    /// The option the offer meets most often, and how many whole times; -1 and 0 when it meets
    /// none. A donation is met once by anything at all.
    /// </summary>
    public static (int RuleIndex, int Times) Evaluate(
        WiredContractTradeRequest request,
        IReadOnlyList<FurnitureItemSnapshot> offer
    )
    {
        if (request.IsDonation)
            return offer.Count > 0 ? (0, 1) : (-1, 0);

        var credits = Credits(offer);
        var counts = offer
            .Where(x => !CreditFurniValue.TryParse(x.Definition.Name, out _))
            .GroupBy(x => ChestItemTypes.Of(x.Definition, x.StuffData))
            .ToDictionary(x => x.Key, x => x.Count());
        var rules = GiveRules(request.Contract);
        var best = (RuleIndex: -1, Times: 0);

        for (var index = 0; index < rules.Count; index++)
        {
            var nodes = rules[index].Nodes;

            if (nodes.IsDefaultOrEmpty)
                continue;

            var times = nodes.Min(node =>
                node.Amount <= 0 ? 0
                : node.Type == TradeRequirementNodeType.Coin ? credits / node.Amount
                : node.ItemType is { } type ? counts.GetValueOrDefault(type) / node.Amount
                : 0
            );

            if (times > best.Times)
                best = (index, times);
        }

        return best;
    }

    /// <summary>How many times the trade goes through when it is confirmed with an offer met this often.</summary>
    public static int TimesToCarryOut(WiredContractTradeRequest request, int timesMet) =>
        request.RulesType switch
        {
            TradeRequirementRulesType.Multiplier => request.Multiplier,
            TradeRequirementRulesType.AutoMultiplier => Math.Min(timesMet, request.Multiplier),
            _ => 1,
        };

    public static bool CanAccept(WiredContractTradeRequest request, int timesMet) =>
        timesMet
        >= (request.RulesType == TradeRequirementRulesType.Multiplier ? request.Multiplier : 1);

    /// <summary>
    /// The offered items the trade takes: all of them for a donation, otherwise each element of
    /// the met option times over, credit furni in offer order until the credits are covered.
    /// </summary>
    public static ImmutableArray<FurnitureItemSnapshot> Take(
        WiredContractTradeRequest request,
        IReadOnlyList<FurnitureItemSnapshot> offer,
        int ruleIndex,
        int times
    )
    {
        if (request.IsDonation)
            return [.. offer];

        var taken = new List<FurnitureItemSnapshot>();
        var rule = GiveRules(request.Contract)[ruleIndex];

        foreach (var node in rule.Nodes)
        {
            var needed = (long)node.Amount * times;

            if (node.Type == TradeRequirementNodeType.Coin)
            {
                var paid = 0L;

                foreach (var item in offer)
                {
                    if (paid >= needed || taken.Contains(item))
                        continue;

                    if (!CreditFurniValue.TryParse(item.Definition.Name, out var value))
                        continue;

                    taken.Add(item);
                    paid += value;
                }

                continue;
            }

            taken.AddRange(
                offer
                    .Where(item =>
                        !taken.Contains(item)
                        && !CreditFurniValue.TryParse(item.Definition.Name, out _)
                        && ChestItemTypes.Of(item.Definition, item.StuffData) == node.ItemType
                    )
                    .Take((int)Math.Min(needed, int.MaxValue))
            );
        }

        return [.. taken];
    }

    /// <summary>What the contract gives, the given number of times; null when it gives nothing.</summary>
    public static TradeRequirementRuleSnapshot? Reward(WiredContractSnapshot contract, int times) =>
        contract.Definition.YouGet is { Nodes.IsDefaultOrEmpty: false } rule
            ? new()
            {
                Nodes =
                [
                    .. rule.Nodes.Select(node =>
                        node with
                        {
                            Amount = (int)Math.Min((long)node.Amount * times, int.MaxValue),
                        }
                    ),
                ],
            }
            : null;

    private static IReadOnlyList<TradeRequirementRuleSnapshot> GiveRules(
        WiredContractSnapshot contract
    ) =>
        contract.Definition.YouGive is { IsDefault: false } rules
            ? rules
            : Array.Empty<TradeRequirementRuleSnapshot>();
}
