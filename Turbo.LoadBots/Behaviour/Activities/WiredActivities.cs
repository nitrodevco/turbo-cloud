using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.LoadBots.Client;
using Turbo.LoadBots.Knowledge;
using Turbo.LoadBots.Metrics;
using Turbo.LoadBots.Protocol;
using Turbo.LoadBots.Protocol.Decoders;
using Turbo.Revisions.Revision20260909;

namespace Turbo.LoadBots.Behaviour.Activities;

/// <summary>Configuring wired, box by box and as working machines.</summary>
public static class WiredActivities
{
    /// <summary>More ints than any box takes (<c>WiredConfig.MaxIntParams</c> is 16).</summary>
    private const int TOO_MANY_INT_PARAMS = 17;

    /// <summary>
    /// The text a free-text box gets. Kept short and plain: some boxes cut or reject long or
    /// formatted text.
    /// </summary>
    private const string BOT_TEXT = "loadbot";

    /// <summary>
    /// Boxes only hotel staff may save (<c>MinimumControllerLevelToSave</c>): a room owner's
    /// save is refused by design, so the bots expect the refusal.
    /// </summary>
    private static readonly HashSet<string> STAFF_ONLY = new(StringComparer.Ordinal)
    {
        "wf_act_give_reward",
    };

    /// <summary>
    /// Boxes the catalog sells that the server has no logic for yet, each waiting on a system
    /// rather than a box (chests, transactions, contracts, the web API; AGENTS.md, "Which boxes
    /// the server still lacks"). They are still tried every run, briefly, so the day one is
    /// written the bots test it; until then their silence is reported, not failed.
    /// </summary>
    private static readonly HashSet<string> KNOWN_GAPS = new(StringComparer.Ordinal)
    {
        "wf_act_cancel_transaction",
        "wf_act_give_currency",
        "wf_act_give_furni",
        "wf_act_init_transaction",
        "wf_cnd_chest_has_item_type",
        "wf_cnd_chest_has_items",
        "wf_trg_transaction_complete",
        "wf_trg_transaction_fail",
        "wf_xtra_custom_contract",
        "wf_xtra_scan_chest_furni_by_type",
        "wf_xtra_var_web_api",
    };

    private static readonly TimeSpan KNOWN_GAP_TIMEOUT = TimeSpan.FromSeconds(2);

    /// <summary>What the report calls a box that is a known gap and still does not open.</summary>
    public const string KNOWN_GAP_OUTCOME = "known gap (no server logic yet)";

    /// <summary>
    /// Takes the next few wired boxes in the fleet's rotation through a full editor round trip,
    /// each on its own tile so no stack ever runs: buy, place, open, save what the box itself
    /// says is valid (its defaults, with real furni picked), open again to see the save kept,
    /// send a save no box accepts and expect it refused, pick the box up.
    /// </summary>
    public static async Task WiredLabAsync(BotContext ctx, CancellationToken ct)
    {
        if (
            await ctx.OwnRoomAsync(RoomPurpose.WiredLab, ct) is not { } roomId
            || !await ctx.EnterAsync(roomId, ct)
        )
            return;

        await EnsureTargetsAsync(ctx, 3, ct);

        foreach (var logic in ctx.World.NextWiredLogics(ctx.Random.Next(3, 8)))
        {
            if (ct.IsCancellationRequested || ctx.Room is null)
                return;

            if (ctx.Knowledge.WiredOffer(logic) is not { } offer)
                continue;

            var outcome = await ExerciseBoxAsync(ctx, offer, ct);
            ctx.World.RecordWiredOutcome(logic, outcome);
        }

        ctx.World.Publish(new PublicRoom(roomId, ctx.Client.Name, RoomPurpose.WiredLab));
    }

    private static async Task<string> ExerciseBoxAsync(
        BotContext ctx,
        FurniOffer offer,
        CancellationToken ct
    )
    {
        var logic = offer.Definition.Logic;
        var placed = await ctx.PlaceSomewhereAsync(offer, ct);

        if (placed is null)
            return "not placed";

        try
        {
            var knownGap = KNOWN_GAPS.Contains(logic);
            var box = knownGap
                ? await ctx.Client.OpenWiredAsync(
                    placed.ObjectId,
                    ct,
                    KNOWN_GAP_TIMEOUT,
                    knownGap: true
                )
                : await ctx.Client.OpenWiredAsync(placed.ObjectId, ct);

            if (box is null && knownGap)
            {
                ctx.Metrics.Count("wired.known_gap_boxes_tried");

                return KNOWN_GAP_OUTCOME;
            }

            // The catalog sells it as wired, so it should open as wired; a box the server has no
            // logic for never answers.
            ctx.Metrics.Check(
                "wired.box.opens_editor",
                box is not null,
                CheckSeverity.Hard,
                $"{logic}: no editor (is the logic implemented?)"
            );

            if (box is null)
                return "editor did not open";

            var save = ValidSave(ctx, box, logic);
            var (saved, error) = await ctx.Client.SaveWiredAsync(save, ct);

            if (STAFF_ONLY.Contains(logic))
            {
                ctx.Metrics.Check(
                    "wired.save.refuses_staff_only_box_to_owner",
                    !saved,
                    CheckSeverity.Hard,
                    $"{logic}: a room owner saved a staff-only box"
                );

                return saved ? "saved by owner (should be staff only)" : "refused (staff only)";
            }

            ctx.Metrics.Check(
                "wired.save.accepts_own_defaults",
                saved,
                CheckSeverity.Hard,
                $"{logic}: {error} ints {WiredDecoders.Describe(save.IntParams)}"
            );

            if (!saved)
                return $"rejected: {error}";

            var reopened = await ctx.Client.OpenWiredAsync(placed.ObjectId, ct);

            if (reopened is not null)
            {
                ctx.Metrics.Check(
                    "wired.save.persists_int_params",
                    WiredDecoders.SameInts(reopened.IntParams, save.IntParams),
                    CheckSeverity.Hard,
                    $"{logic}: sent {WiredDecoders.Describe(save.IntParams)} read {WiredDecoders.Describe(reopened.IntParams)}"
                );
                ctx.Metrics.Check(
                    "wired.save.persists_picked_furni",
                    save.StuffIds.All(reopened.StuffIds.Contains),
                    CheckSeverity.Hard,
                    $"{logic}: sent {WiredDecoders.Describe(save.StuffIds)} read {WiredDecoders.Describe(reopened.StuffIds)}"
                );
            }

            var invalid = save with { IntParams = [.. Enumerable.Repeat(0, TOO_MANY_INT_PARAMS)] };
            var (acceptedInvalid, _) = await ctx.Client.SaveWiredAsync(invalid, ct);

            ctx.Metrics.Check(
                "wired.save.rejects_too_many_int_params",
                !acceptedInvalid,
                CheckSeverity.Hard,
                $"{logic}: took {TOO_MANY_INT_PARAMS} int params"
            );

            return "saved";
        }
        finally
        {
            await ctx.Client.PickupAsync(placed.ObjectId, ct);
        }
    }

    /// <summary>
    /// A save the box should take: its own default ints, its default sources, real furni from
    /// the room for the furni it picks, and text where it reads text.
    /// </summary>
    public static WiredSave ValidSave(BotContext ctx, WiredBox box, string logic)
    {
        var picks = box.FurniLimit > 0 ? Targets(ctx, Math.Min(box.FurniLimit, 2)) : [];

        return Save(box, logic, picks);
    }

    public static WiredSave Save(WiredBox box, string logic, IReadOnlyList<int> picks) =>
        new()
        {
            Kind = box.Kind,
            ObjectId = box.ObjectId,
            IntParams = box.DefaultIntParams.Count > 0 ? box.DefaultIntParams : box.IntParams,
            StringParam = TakesText(logic) ? BOT_TEXT : box.StringParam,
            StuffIds = picks,
            Delay = 0,
            Quantifier = box.Kind is WiredKind.Condition ? box.DefinitionInt : 0,
            Filter = box.SelectorFilter,
            Invert = box.SelectorInvert,
            FurniSources = box.DefaultFurniSources,
            UserSources = box.DefaultUserSources,
            VariableIds = box.VariableIds,
        };

    private static bool TakesText(string logic) =>
        logic.Contains("show_message", StringComparison.Ordinal)
        || logic.Contains("says_something", StringComparison.Ordinal)
        || logic.Contains("kick_user", StringComparison.Ordinal)
        || logic.Contains("bot_talk", StringComparison.Ordinal);

    /// <summary>Plain furni in the room for wired to pick, not wired themselves.</summary>
    private static List<int> Targets(BotContext ctx, int count) =>
        [
            .. (ctx.Room?.Items() ?? [])
                .Where(x => ctx.Knowledge.FloorDefinition(x.SpriteId) is { IsWired: false })
                .OrderBy(_ => ctx.Random.Next())
                .Take(count)
                .Select(x => x.ObjectId),
        ];

    private static async Task EnsureTargetsAsync(BotContext ctx, int count, CancellationToken ct)
    {
        var have = Targets(ctx, count).Count;

        for (var i = have; i < count && ctx.Knowledge.DecorOffers.Count > 0; i++)
            await ctx.PlaceSomewhereAsync(ctx.Pick(ctx.Knowledge.DecorOffers), ct);
    }

    /// <summary>
    /// A wired machine that runs on its own: every second it shoves a furni a tile in a random
    /// direction. It keeps the room busy for everyone in it, which is the point under load, and
    /// the bot checks the furni does move.
    /// </summary>
    public static async Task BuildContraptionAsync(BotContext ctx, CancellationToken ct)
    {
        if (
            await ctx.OwnRoomAsync(RoomPurpose.Architecture, ct) is not { } roomId
            || !await ctx.EnterAsync(roomId, ct)
        )
            return;

        if (
            ctx.Knowledge.WiredOffer("wf_trg_periodically") is not { } triggerOffer
            || ctx.Knowledge.WiredOffer("wf_act_move_rotate") is not { } actionOffer
        )
            return;

        await EnsureTargetsAsync(ctx, 2, ct);

        var target = Targets(ctx, 1);

        if (target.Count == 0)
            return;

        // Both boxes on one tile make one stack.
        var trigger = await ctx.PlaceSomewhereAsync(triggerOffer, ct);

        if (trigger is null)
            return;

        var actionItem = await ctx.ObtainAsync(actionOffer, ct);

        if (actionItem is null)
            return;

        var action = await ctx.Client.PlaceAsync(actionItem, trigger.X, trigger.Y, 0, ct);

        if (action is null)
            return;

        // Every 2 pulses (a second); move in a random direction (1), no turn (0).
        var savedTrigger = await ConfigureAsync(
            ctx,
            trigger.ObjectId,
            "wf_trg_periodically",
            [2],
            [],
            ct
        );
        var savedAction = await ConfigureAsync(
            ctx,
            action.ObjectId,
            "wf_act_move_rotate",
            [1, 0],
            target,
            ct
        );

        if (!savedTrigger || !savedAction)
            return;

        var moved = await ctx.Client.WaitForAsync(
            m =>
                m.Header
                    is MessageComposer.WiredMovementsMessageComposer
                        or MessageComposer.SlideObjectBundleMessageComposer
                || m.Header == MessageComposer.ObjectUpdateMessageComposer
                    && RoomDecoders.ObjectUpdate(m.Reader()).ObjectId == target[0],
            TimeSpan.FromSeconds(6),
            "contraption movement",
            ct
        );

        ctx.Metrics.Check(
            "wired.contraption.moves_furni",
            moved,
            CheckSeverity.Soft,
            $"{ctx.Client.Name}: room {roomId}"
        );

        ctx.World.Publish(new PublicRoom(roomId, ctx.Client.Name, RoomPurpose.Architecture));
    }

    /// <summary>Opens a box and saves it with these ints and picks, keeping its other defaults.</summary>
    public static async Task<bool> ConfigureAsync(
        BotContext ctx,
        int objectId,
        string logic,
        IReadOnlyList<int> intParams,
        IReadOnlyList<int> picks,
        CancellationToken ct,
        string? text = null,
        IReadOnlyList<int>? userSources = null
    )
    {
        var box = await ctx.Client.OpenWiredAsync(objectId, ct);

        if (box is null)
            return false;

        var save = Save(box, logic, picks) with { IntParams = intParams };

        if (text is not null)
            save = save with { StringParam = text };

        if (userSources is not null)
            save = save with { UserSources = userSources };

        var (saved, error) = await ctx.Client.SaveWiredAsync(save, ct);

        ctx.Metrics.Check(
            "wired.save.accepts_configured",
            saved,
            CheckSeverity.Hard,
            $"{logic}: {error} ints {WiredDecoders.Describe(intParams)}"
        );

        return saved;
    }
}
