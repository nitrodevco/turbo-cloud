using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A crackable furni: the client's <c>furniture_crackable</c>, and every crackable class of
/// Sulake's furni data - the Habbo Club and Builders Club boxes, eggs, crystals, piñatas, bonus
/// bags, plants grown with the watering can. How each one opens is its definition's
/// <see cref="CrackableData"/>.
/// <para>
/// Its stuff data is Flash's <c>CrackableStuffData</c>: the state it draws, the hits it has taken
/// and the hits that crack it, which the infostand shows as "Hits: x / y" under a Use button.
/// Who may hit it is the definition's usage policy, which is Sulake's: everyone for the furni its
/// data marks <c>&lt;everyone-can-use/&gt;</c> (eggs, crystals, piñatas, the public ones, some
/// plants - "help other farmers by watering their crops"), room rights for the rest. A use from
/// beside it is a hit, and from further away it walks the avatar over; a piñata is hit by walking
/// under it instead. A required effect (the watering can, the magic wand, the piñata stick) has to
/// be worn. The hits move it through its states, the last of which -
/// its opening, which the asset plays once and then draws nothing - only the cracking hit
/// reaches. It stands in that state for <c>CrackableOpenMs</c>, then it is gone and what it held
/// is handed over: a membership, or a furni drawn from its rewards, on its tile or into the
/// inventory.
/// </para>
/// <para>
/// The hits live in the item's stuff data, so they stay with it through a pickup, a trade and a
/// restart; the state is always worked out again from them, so an item that a logic without
/// hits left in another state (a box toggled to its empty opening frame) draws whole again.
/// A crackable with nothing to give is never cracked: the hit that would crack it is refused and
/// logged, rather than the furni disappearing for nothing.
/// </para>
/// </summary>
[RoomObjectLogic("crackable")]
public class FurnitureCrackableLogic : FurnitureFloorLogic
{
    protected override StuffDataType _stuffDataType => StuffDataType.CrackableKey;

    private readonly CrackableData _data;

    /// <summary>The cracking hit landed and the opening is playing; no more hits count.</summary>
    private bool _opening;

    public FurnitureCrackableLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
        : base(stuffDataFactory, ctx)
    {
        _data =
            FurnitureExtraDataSections.Read<CrackableData>(
                ctx.RoomObject.ExtraData,
                ctx.Definition.ExtraData,
                CrackableData.SECTION,
                _roomGrain._logger
            ) ?? new CrackableData();

        if (StuffData is ICrackableStuffData crackable)
        {
            var hits = Math.Clamp(crackable.Hits, 0, Target);

            crackable.SetProgress(hits, Target, StateFor(hits));
        }
    }

    public int Target => Math.Max(1, _data.Target);

    public int Hits => StuffData is ICrackableStuffData crackable ? crackable.Hits : 0;

    public bool IsCracked => Hits >= Target;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (_data.HitOn != CrackableHitOn.Use || GetAvatar(ctx) is not { } avatar)
            return;

        if (!FloorFootprint.Of(_ctx.RoomObject).IsOnOrNextTo(avatar.X, avatar.Y))
        {
            await WalkBesideAsync(avatar, ct);

            return;
        }

        await HitAsync(ctx, avatar, ct);
    }

    public override async Task OnWalkOnAsync(IRoomAvatarContext ctx, CancellationToken ct)
    {
        await base.OnWalkOnAsync(ctx, ct);

        if (_data.HitOn != CrackableHitOn.Walk || ctx.RoomObject is not IRoomPlayer player)
            return;

        var hitter = ActionContext.CreateForPlayer(player.PlayerId, _ctx.RoomId);

        // The use path asks this of every use; a walk-on has to ask it here.
        if (!await SecurityModule.CanUseFurniAsync(hitter, GetUsagePolicy()))
            return;

        await HitAsync(hitter, player, ct);
    }

    /// <summary>One hit by an avatar standing where it may hit from.</summary>
    private async Task HitAsync(ActionContext ctx, IRoomAvatar avatar, CancellationToken ct)
    {
        if (_opening)
            return;

        if (_data.RequiredEffectId > 0 && avatar.EffectId != _data.RequiredEffectId)
            return;

        // An item picked up while it was opening keeps its cracking hit; the next use opens it.
        if (IsCracked)
        {
            Open(ctx);

            return;
        }

        if (Hits + 1 >= Target && !_data.HasReward)
        {
            _roomGrain._logger.LogWarning(
                "Crackable {ItemId} ({Definition}, reward set {RewardSet}) in room {RoomId} holds nothing the room can give, so it is not cracked",
                _ctx.ObjectId,
                _ctx.Definition.Name,
                _data.RewardSet,
                _ctx.RoomId
            );

            return;
        }

        var hits = Hits + 1;

        if (StuffData is ICrackableStuffData crackable)
            crackable.SetProgress(hits, Target, StateFor(hits));

        PersistStuffData(true);

        await OnStateChangedAsync(ct);

        await RecordAchievementsAsync(ctx, cracked: hits >= Target, ct);

        if (hits >= Target)
            Open(ctx);
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _opening = false;

        return base.OnPickupAsync(ctx, ct);
    }

    /// <summary>
    /// The state a number of hits draws: the hits spread evenly over every state but the last,
    /// and the last - the opening - for the cracking hit alone. A box with three states and one
    /// hit goes 0 to 2; an egg with fifteen states and a thousand hits cracks a little every
    /// 72 hits.
    /// </summary>
    public int StateFor(int hits)
    {
        var lastState = Math.Max(0, _ctx.Definition.TotalStates - 1);

        if (hits >= Target)
            return lastState;

        return Math.Clamp(hits * lastState / Target, 0, Math.Max(0, lastState - 1));
    }

    private void Open(ActionContext cracker)
    {
        _opening = true;

        TimerSystem.Schedule(
            _ctx.ObjectId,
            _roomGrain._roomConfig.CrackableOpenMs,
            token => OpenedAsync(cracker, token)
        );
    }

    /// <summary>
    /// The opening has played. The furni goes first and what it held is handed over only once it
    /// is gone, so a crackable can never give twice.
    /// </summary>
    private async Task OpenedAsync(ActionContext cracker, CancellationToken ct)
    {
        _opening = false;

        var ownerId = _ctx.RoomObject.OwnerId;
        var owner = ActionContext.CreateForPlayer(ownerId, _ctx.RoomId);
        var recipient = _data.RewardTo == CrackableRecipient.Cracker ? cracker.PlayerId : ownerId;
        var (x, y, rotation) = (_ctx.RoomObject.X, _ctx.RoomObject.Y, _ctx.RoomObject.Rotation);

        // Removed as its owner, who may always remove their own furni, whoever cracked it.
        if (!await ActionModule.DeleteItemByIdAsync(owner, _ctx.ObjectId, ct))
            return;

        if (DrawReward() is { } reward)
            await GiveAsync(recipient, reward, x, y, rotation, ct);

        _roomGrain._logger.LogInformation(
            "Crackable {ItemId} ({Definition}, reward set {RewardSet}) in room {RoomId} was cracked by player {PlayerId} for player {RecipientId}",
            _ctx.ObjectId,
            _ctx.Definition.Name,
            _data.RewardSet,
            _ctx.RoomId,
            cracker.PlayerId,
            recipient
        );
    }

    /// <summary>One of the rewards, drawn by weight; null when it holds none.</summary>
    private CrackableReward? DrawReward()
    {
        var rewards = _data.Rewards.Where(x => x.IsValid).ToArray();
        var total = rewards.Sum(x => x.Weight);

        if (total <= 0)
            return null;

        var roll = Random.Shared.Next(total);

        foreach (var reward in rewards)
        {
            if (roll < reward.Weight)
                return reward;

            roll -= reward.Weight;
        }

        return rewards[^1];
    }

    /// <summary>
    /// Gives what was drawn: a membership, credits, or a furni - into the recipient's inventory,
    /// and from there onto the tile the crackable stood on when it is to go in the room and fits.
    /// </summary>
    private async Task GiveAsync(
        PlayerId recipient,
        CrackableReward reward,
        int x,
        int y,
        Rotation rotation,
        CancellationToken ct
    )
    {
        if (reward is { Subscription: { } subscription, SubscriptionDays: > 0 })
            await _roomGrain
                ._grainFactory.GetPlayerSubscriptionGrain(recipient)
                .ExtendAsync(subscription, reward.SubscriptionDays, ct);

        if (reward.Credits > 0)
            await _roomGrain
                ._grainFactory.GetPlayerWalletGrain(recipient)
                .CreditAsync(CurrencyKind.Credits, reward.Credits, ct);

        if (string.IsNullOrWhiteSpace(reward.Furni))
            return;

        if (
            _roomGrain._definitionProvider.TryGetDefinitionByName(reward.Furni)
            is not { } definition
        )
        {
            _roomGrain._logger.LogWarning(
                "Crackable {ItemId} ({Definition}) names reward {Reward}, which is no furni definition",
                _ctx.ObjectId,
                _ctx.Definition.Name,
                reward.Furni
            );

            return;
        }

        var item = await _roomGrain
            ._grainFactory.GetInventoryGrain(recipient)
            .GrantFurnitureAsync(definition.Id, null, ct);

        if (
            item is null
            || _data.RewardPlacement != CrackablePlacement.Room
            || definition.ProductType != ProductType.Floor
        )
            return;

        try
        {
            await ActionModule.PlaceFloorItemAsync(
                ActionContext.CreateForPlayer(recipient, _ctx.RoomId),
                item,
                x,
                y,
                rotation,
                ct
            );
        }
        catch (TurboException ex)
        {
            // It does not fit there, or the recipient may not place furni in this room.
            _roomGrain._logger.LogDebug(
                ex,
                "Reward {ItemId} of crackable {CrackableId} stays in the inventory of player {PlayerId}",
                item.ItemId,
                _ctx.ObjectId,
                recipient
            );
        }
    }

    /// <summary>
    /// The hitter's progress: every hit towards the incremental achievement, the cracking hit
    /// towards the final one. Each is a fact the achievement system binds by its value.
    /// </summary>
    private async Task RecordAchievementsAsync(
        ActionContext ctx,
        bool cracked,
        CancellationToken ct
    )
    {
        var hit = AchievementName(_data.IncrementalHitAchievement);
        var final = cracked ? AchievementName(_data.FinalHitAchievement) : null;

        if (ctx.Origin != ActionOrigin.Player || (hit is null && final is null))
            return;

        await using var db = await _roomGrain._dbCtxFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;

        if (hit is not null)
            _roomGrain._achievementFacts.Record(
                db,
                ctx.PlayerId,
                new()
                {
                    Source = AchievementSources.CRACKABLE_HIT,
                    Value = hit,
                    OperationId = Guid.NewGuid().ToString("N"),
                    OccurredAtUtc = now,
                }
            );

        if (final is not null)
            _roomGrain._achievementFacts.Record(
                db,
                ctx.PlayerId,
                new()
                {
                    Source = AchievementSources.CRACKABLE_CRACKED,
                    Value = final,
                    Amount = Math.Max(1, _data.FinalHitAchievementCount),
                    OperationId = Guid.NewGuid().ToString("N"),
                    OccurredAtUtc = now,
                }
            );

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Sulake's data spells some names in lower case (<c>pinatawhacker</c>); facts carry them so.</summary>
    private static string? AchievementName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim().ToLowerInvariant();

    /// <summary>
    /// Walks the avatar to the tile beside the furni nearest to them, as it is hit from next to
    /// it; a tile no path reaches is passed over for the next nearest.
    /// </summary>
    private async Task WalkBesideAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        var footprint = FloorFootprint.Of(_ctx.RoomObject);
        var map = MapModule;

        var tiles = footprint
            .Tiles()
            .SelectMany(tile =>
                from dx in new[] { -1, 0, 1 }
                from dy in new[] { -1, 0, 1 }
                select (X: tile.X + dx, Y: tile.Y + dy)
            )
            .Distinct()
            .Where(tile =>
                map.InBounds(tile.X, tile.Y) && footprint.DistanceTo(tile.X, tile.Y) == 1
            )
            .OrderBy(tile => Math.Max(Math.Abs(tile.X - avatar.X), Math.Abs(tile.Y - avatar.Y)))
            .ThenBy(tile =>
                (tile.X - avatar.X) * (tile.X - avatar.X)
                + (tile.Y - avatar.Y) * (tile.Y - avatar.Y)
            )
            .ToArray();

        foreach (var (x, y) in tiles)
        {
            if (await AvatarModule.WalkAvatarToAsync(avatar, x, y, ct))
                return;
        }
    }
}
