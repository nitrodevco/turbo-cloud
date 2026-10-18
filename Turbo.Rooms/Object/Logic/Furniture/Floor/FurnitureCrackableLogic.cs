using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
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
/// beside it is a hit, and a use from further away walks the avatar over and hits once it gets
/// there, as Habbo does with one Use; a piñata is hit by walking under it instead. A required
/// effect (the watering can, the magic wand, the piñata stick) has to be worn. The hits move it
/// through its states, the last of which - its opening, which the asset plays once and then draws
/// nothing - only the cracking hit reaches. The cracking hit removes it at once, with a delay of
/// <c>CrackableRemoveDelayMs</c> the client waits out while the opening plays, and hands over
/// what it held straight after: a membership, or a furni drawn from its rewards, on its tile or
/// into the inventory.
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

    /// <summary>Avatars sent walking over by a use, with the tile beside it they were sent to.</summary>
    private readonly Dictionary<RoomObjectId, (int TileIdx, ActionContext Hitter)> _approaching =
    [];

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
            if (await WalkBesideAsync(avatar, ct) is not { } tileIdx)
                return;

            _approaching[avatar.ObjectId] = (tileIdx, ctx);
            ScheduleTick();

            return;
        }

        _approaching.Remove(avatar.ObjectId);

        await HitAsync(ctx, avatar, ct);
    }

    private void ScheduleTick() =>
        TimerSystem.Schedule(_ctx.ObjectId, _roomGrain._roomConfig.AvatarTickMs, TickAsync);

    /// <summary>
    /// Hits for whoever a use sent over once they step onto the tile they were sent to - Habbo's
    /// hit lands as the avatar takes that last step, before it stands still - and forgets whoever
    /// stopped short or was sent elsewhere; runs while anyone is on their way.
    /// </summary>
    private async Task TickAsync(CancellationToken ct)
    {
        foreach (var (objectId, (tileIdx, hitter)) in _approaching.ToArray())
        {
            if (!AvatarModule.TryGetAvatar(objectId, out var avatar))
            {
                _approaching.Remove(objectId);

                continue;
            }

            var tile =
                avatar.NextTileId >= 0 ? avatar.NextTileId : MapModule.ToIdx(avatar.X, avatar.Y);

            if (tile == tileIdx)
            {
                _approaching.Remove(objectId);

                await HitAsync(hitter, avatar, ct);

                // The cracking hit removed it; nothing is left to tick.
                if (IsCracked)
                    return;
            }
            else if (!avatar.IsWalking || avatar.GoalTileId != tileIdx)
                _approaching.Remove(objectId);
        }

        if (_approaching.Count > 0)
            ScheduleTick();
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
        // A borrowed or temporary crackable holds no prize: nothing in it was ever paid for.
        if (IsBorrowedOrTemporary || !_data.AcceptsEffect(avatar.EffectId))
            return;

        // An item left holding its cracking hit (picked up while it opened, before the opening
        // went without a wait) opens on the next use.
        if (IsCracked)
        {
            await OpenAsync(ctx, ct);

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
            await OpenAsync(ctx, ct);
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _approaching.Clear();

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

    /// <summary>
    /// Cracked: the furni goes first and what it held is handed over only once it is gone, so a
    /// crackable can never give twice. Both happen at once, as on Habbo, where the removal
    /// follows the cracking hit's state by 25 ms and the membership straight after it; the
    /// removal's delay is what keeps the opening on screen.
    /// </summary>
    private async Task OpenAsync(ActionContext cracker, CancellationToken ct)
    {
        var ownerId = _ctx.RoomObject.OwnerId;
        var owner = ActionContext.CreateForPlayer(ownerId, _ctx.RoomId);
        var recipient = _data.RewardTo == CrackableRecipient.Cracker ? cracker.PlayerId : ownerId;
        var (x, y, rotation) = (_ctx.RoomObject.X, _ctx.RoomObject.Y, _ctx.RoomObject.Rotation);

        // Removed as its owner, who may always remove their own furni, whoever cracked it; and
        // picked up by nobody (Habbo's pickerId -1), so nothing flies to an avatar.
        if (
            !await ActionModule.DeleteItemByIdAsync(
                owner,
                _ctx.ObjectId,
                MovementCause.New(
                    MovementCauses.USE_CRACKABLE,
                    owner.PlayerId,
                    _ctx.ObjectId.Value
                ),
                ct,
                removeDelayMs: _roomGrain._roomConfig.CrackableRemoveDelayMs,
                byOwner: false
            )
        )
            return;

        foreach (var reward in DrawRewards())
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
    /// <summary>What it gives: one draw from its rewards, or each of its draws.</summary>
    private IEnumerable<CrackableReward> DrawRewards()
    {
        if (_data.Draws.IsDefaultOrEmpty)
        {
            if (Draw(_data.Rewards) is { } reward)
                yield return reward;

            yield break;
        }

        foreach (var draw in _data.Draws)
        {
            for (var i = 0; i < draw.Count; i++)
            {
                if (Draw(draw.Rewards) is { } reward)
                    yield return reward;
            }
        }
    }

    private static CrackableReward? Draw(ImmutableArray<CrackableReward> pool)
    {
        if (pool.IsDefaultOrEmpty)
            return null;

        var rewards = pool.Where(x => x.IsValid).ToArray();
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

        // One operation for the whole prize: the credits first, then each furni.
        var cause = MovementCause.New(
            MovementCauses.USE_CRACKABLE,
            recipient.Value,
            _ctx.ObjectId.Value
        );

        if (reward.Credits > 0)
            await _roomGrain
                ._grainFactory.GetPlayerWalletGrain(recipient)
                .CreditAsync(CurrencyKind.Credits, reward.Credits, cause, ct);

        if (string.IsNullOrWhiteSpace(reward.Furni))
            return;

        var seq = 1;

        await GiveFurniAsync(recipient, reward.Furni, x, y, rotation, cause.StartingAt(seq++), ct);

        if (reward.Also.IsDefaultOrEmpty)
            return;

        foreach (var also in reward.Also)
            await GiveFurniAsync(recipient, also, x, y, rotation, cause.StartingAt(seq++), ct);
    }

    /// <summary>
    /// Gives one furni of a reward: into the recipient's inventory, and from there onto the tile
    /// the crackable stood on when it is to go in the room and fits there.
    /// </summary>
    private async Task GiveFurniAsync(
        PlayerId recipient,
        string name,
        int x,
        int y,
        Rotation rotation,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (_roomGrain._definitionProvider.TryGetDefinitionByName(name) is not { } definition)
        {
            _roomGrain._logger.LogWarning(
                "Crackable {ItemId} ({Definition}) names reward {Reward}, which is no furni definition",
                _ctx.ObjectId,
                _ctx.Definition.Name,
                name
            );

            return;
        }

        var item = await _roomGrain
            ._grainFactory.GetInventoryGrain(recipient)
            .GrantFurnitureAsync(definition.Id, null, cause, ct);

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
    /// it; a tile no path reaches is passed over for the next nearest. The tile it was sent to, or
    /// null when none is reached.
    /// </summary>
    private async Task<int?> WalkBesideAsync(IRoomAvatar avatar, CancellationToken ct)
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
                return map.ToIdx(x, y);
        }

        return null;
    }
}
