using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

/// <summary>
/// A wired contract: what a player pays, trades or is rewarded when an Initiate Transaction box
/// offers it. The contract is the furni's extra data (<see cref="WiredContractData"/>); anyone
/// who may read the room's wired opens it, and whoever may modify the room's wired saves it,
/// within the limits the client's editor draws (<see cref="Configuration.WiredChestConfig"/>).
/// </summary>
public abstract class FurnitureWiredContractLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const string GENERIC_LAYOUT = "generic";
    private const string GAMES_LAYOUT = "games";

    public abstract WiredContractType ContractType { get; }

    /// <summary>The contract as saved, or the empty one of its type the editor starts from.</summary>
    public WiredContractSnapshot Contract =>
        (
            FurnitureExtraDataSections
                .Read<WiredContractData>(
                    _ctx.RoomObject.ExtraData,
                    WiredContractData.SECTION,
                    _roomGrain._logger
                )
                ?.Contract
            ?? Empty()
        ) with
        {
            ContractId = _ctx.ObjectId,
            Type = ContractType,
        };

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Everybody;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (!(await GetPermissionsAsync(ctx)).CanRead)
            return;

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new WiredOpenContractMessageComposer { ContractId = _ctx.ObjectId },
            ct
        );
    }

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        switch (interaction)
        {
            case OpenContractInteraction:
                if (!(await GetPermissionsAsync(ctx)).CanRead)
                    return Reject(ctx, interaction, "may not read wired");

                await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                    ctx.PlayerId,
                    new WiredContractContentsMessageComposer { Contract = Contract },
                    ct
                );

                return true;
            case UpdateContractInteraction update:
                if (!(await GetPermissionsAsync(ctx)).CanModify)
                    return Reject(ctx, interaction, "may not modify wired");

                var saved = Normalize(update.Contract);

                if (saved is not null)
                    _ctx.RoomObject.ExtraData.UpdateSection(
                        WiredContractData.SECTION,
                        new WiredContractData { Contract = saved }
                    );

                await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                    ctx.PlayerId,
                    new WiredContractUpdateResultMessageComposer
                    {
                        ContractId = _ctx.ObjectId,
                        IsSuccess = saved is not null,
                        FailCode = saved is null
                            ? WiredContractFailCodes.INVALID_RULES
                            : string.Empty,
                    },
                    ct
                );

                return saved is not null;
            default:
                return false;
        }
    }

    /// <summary>
    /// The contract as it may be kept, or null when it breaks the editor's rules: the right
    /// sides for its type, at most the configured options and elements, amounts within bounds,
    /// and only furni that exists, may be traded and is not credit furni.
    /// </summary>
    private WiredContractSnapshot? Normalize(WiredContractSnapshot proposed)
    {
        var config = _roomGrain._wiredChestConfig;

        if (proposed.Type != ContractType)
            return null;

        var give = proposed.Definition.YouGive is { IsDefault: false } rules ? rules : [];
        var get = proposed.Definition.YouGet;

        var needsGive =
            ContractType == WiredContractType.Trade
            || (
                ContractType == WiredContractType.Payment
                && proposed.PaymentMode == WiredContractPaymentMode.Specific
            );

        if (
            give.Length > config.MaxContractRules
            || (needsGive && give.Length == 0)
            || give.Any(rule => !IsValid(rule))
        )
            return null;

        if (ContractType != WiredContractType.Payment && (get is null || !IsValid(get)))
            return null;

        return new()
        {
            ContractId = _ctx.ObjectId,
            Type = ContractType,
            Definition = new()
            {
                YouGive = ContractType == WiredContractType.Reward ? [] : give,
                YouGet = ContractType == WiredContractType.Payment ? null : get,
            },
            PaymentMode = Enum.IsDefined(proposed.PaymentMode)
                ? proposed.PaymentMode
                : WiredContractPaymentMode.Donation,
            ReceiveText = Cut(proposed.ReceiveText, config.ContractReceiveTextMaxLength),
            LayoutType = proposed.LayoutType == GAMES_LAYOUT ? GAMES_LAYOUT : GENERIC_LAYOUT,
            RewardCategory = Enum.IsDefined((WiredEarningsCategory)proposed.RewardCategory)
                ? proposed.RewardCategory
                : (int)WiredEarningsCategory.Games,
            ShowDialog = proposed.ShowDialog,
            RewardText = Cut(proposed.RewardText, config.ContractRewardTextMaxLength),
        };
    }

    private bool IsValid(TradeRequirementRuleSnapshot rule)
    {
        var config = _roomGrain._wiredChestConfig;

        if (rule.Nodes.IsDefaultOrEmpty || rule.Nodes.Length > config.MaxContractNodes)
            return false;

        return rule.Nodes.All(node =>
            node.Amount >= 1
            && (
                node.Type == TradeRequirementNodeType.Coin
                    ? node.Amount <= config.MaxContractCoins
                    : node.Amount <= config.MaxContractFurni && IsTradeable(node.ItemType)
            )
        );
    }

    private bool IsTradeable(ChestItemTypeSnapshot? type) =>
        type is not null
        && _roomGrain._definitionProvider.TryGetDefinitionBySprite(
            type.IsWallItem
                ? Primitives.Furniture.Enums.ProductType.Wall
                : Primitives.Furniture.Enums.ProductType.Floor,
            type.TypeId
        )
            is { CanTrade: true } definition
        && !CreditFurniValue.TryParse(definition.Name, out _);

    private async Task<(bool CanModify, bool CanRead)> GetPermissionsAsync(ActionContext ctx) =>
        WiredSystem.GetPermissions(await SecurityModule.GetControllerLevelAsync(ctx));

    /// <summary>The empty contract of this type; the client draws nothing for a missing side.</summary>
    private WiredContractSnapshot Empty() =>
        new()
        {
            ContractId = _ctx.ObjectId,
            Type = ContractType,
            Definition = new()
            {
                YouGive = ContractType == WiredContractType.Reward ? null : [],
                YouGet = ContractType == WiredContractType.Payment ? null : new() { Nodes = [] },
            },
            LayoutType = GENERIC_LAYOUT,
            RewardCategory = (int)WiredEarningsCategory.Games,
            // Habbo's editor opens a new reward contract with "Show pop-up by default" ticked.
            ShowDialog = ContractType == WiredContractType.Reward,
        };

    private static string Cut(string? text, int length) =>
        text is null ? string.Empty
        : text.Length <= length ? text
        : text[..length];
}
