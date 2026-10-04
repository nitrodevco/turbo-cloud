using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.LoadBots.Knowledge;
using Turbo.LoadBots.Metrics;
using Turbo.LoadBots.Protocol.Decoders;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.LoadBots.Behaviour.Activities;

/// <summary>
/// A pad game made of wired: step on a pad to score and be told so; the host's game timer
/// starts a round (everyone is told, and put on a team) and ends it (everyone is told).
/// Players check every one of those messages reaches them, so a round is a working test of
/// triggers, actions, sources, teams and the game system under however many players join.
/// </summary>
public static class GameActivities
{
    private const int PAD_COUNT = 4;

    // show_message: visibility (0 the selected users, 1 everyone), bubble style, bubble width.
    private const int STYLE = 34;

    private static readonly int[] TO_TRIGGERER =
    [
        (int)WiredChatVisibilityType.SelectedUsersOnly,
        STYLE,
        -1,
    ];
    private static readonly int[] TO_EVERYONE = [(int)WiredChatVisibilityType.Everyone, STYLE, -1];
    private static readonly int[] ALL_ROOM_USERS = [(int)WiredSourceType.AllRoomUsers];

    private static readonly TimeSpan ROUND_TIMEOUT = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Hosts: builds the game the first time, then runs a round each time it is chosen.
    /// </summary>
    public static async Task HostAsync(BotContext ctx, CancellationToken ct)
    {
        if (
            await ctx.OwnRoomAsync(RoomPurpose.Game, ct) is not { } roomId
            || !await ctx.EnterAsync(roomId, ct)
        )
            return;

        var game = ctx.World.Game(roomId) ?? await BuildAsync(ctx, roomId, ct);

        if (game is null)
            return;

        await RunRoundAsync(ctx, game, ct);
    }

    private static async Task<GameRoom?> BuildAsync(
        BotContext ctx,
        int roomId,
        CancellationToken ct
    )
    {
        var knowledge = ctx.Knowledge;

        if (
            knowledge.WalkablePadOffers.Count == 0
            || knowledge.GameCounterOffer is not { } counterOffer
            || knowledge.WiredOffer("wf_trg_walks_on_furni") is not { } walksOn
            || knowledge.WiredOffer("wf_trg_game_starts") is not { } gameStarts
            || knowledge.WiredOffer("wf_trg_game_ends") is not { } gameEnds
            || knowledge.WiredOffer("wf_act_show_message") is not { } showMessage
            || knowledge.WiredOffer("wf_act_give_score") is not { } giveScore
            || knowledge.WiredOffer("wf_act_join_team") is not { } joinTeam
        )
            return null;

        // Start from an empty room so an earlier build's stacks do not double every message.
        var room = ctx.Room!;

        foreach (var item in room.Items().Where(x => x.OwnerId == ctx.Client.PlayerId))
            await ctx.Client.PickupAsync(item.ObjectId, ct);

        var padOffer = ctx.Pick(knowledge.WalkablePadOffers);
        var pads = new List<FloorItem>();

        for (var i = 0; i < PAD_COUNT; i++)
        {
            if (await ctx.PlaceSomewhereAsync(padOffer, ct) is { } pad)
                pads.Add(pad);
        }

        var counter = await ctx.PlaceSomewhereAsync(counterOffer, ct);

        if (pads.Count == 0 || counter is null)
            return null;

        var suffix = roomId.ToString(CultureInfo.InvariantCulture);
        var goalText = $"goal {suffix}";
        var startText = $"start {suffix}";
        var endText = $"over {suffix}";
        var padIds = pads.Select(x => x.ObjectId).ToList();
        var team = ctx.Random.Next((int)GameTeamType.Red, (int)GameTeamType.Yellow + 1);

        var built =
            await StackAsync(
                ctx,
                new Box(walksOn, [], padIds, null, null),
                new Box(giveScore, [1, 0], [], null, null),
                ct,
                new Box(showMessage, TO_TRIGGERER, [], goalText, null)
            )
            && await StackAsync(
                ctx,
                new Box(gameStarts, [], [], null, null),
                new Box(showMessage, TO_EVERYONE, [], startText, ALL_ROOM_USERS),
                ct,
                new Box(joinTeam, [team, 0], [], null, ALL_ROOM_USERS)
            )
            && await StackAsync(
                ctx,
                new Box(gameEnds, [], [], null, null),
                new Box(showMessage, TO_EVERYONE, [], endText, ALL_ROOM_USERS),
                ct
            );

        ctx.Metrics.Check(
            "game.build.completes",
            built,
            CheckSeverity.Hard,
            $"{ctx.Client.Name}: room {roomId}"
        );

        if (!built)
            return null;

        var game = new GameRoom(
            roomId,
            ctx.Client.Name,
            [.. pads.Select(x => (x.X, x.Y))],
            goalText,
            startText,
            endText,
            counter.ObjectId
        );

        ctx.World.PublishGame(game);

        await ctx.Client.SayAsync("Game room open, step on the pads!", ct);

        return game;
    }

    /// <summary>
    /// Places a trigger on a free tile and the other boxes on top of it, configuring each.
    /// A box is (offer, ints, picked furni, text, user sources).
    /// </summary>
    private static async Task<bool> StackAsync(
        BotContext ctx,
        Box trigger,
        Box action,
        CancellationToken ct,
        params Box[] more
    )
    {
        var placedTrigger = await ctx.PlaceSomewhereAsync(trigger.Offer, ct);

        if (placedTrigger is null)
            return false;

        if (!await ConfigureAsync(ctx, placedTrigger.ObjectId, trigger, ct))
            return false;

        foreach (var box in (Box[])[action, .. more])
        {
            var item = await ctx.ObtainAsync(box.Offer, ct);

            if (item is null)
                return false;

            var placed = await ctx.Client.PlaceAsync(item, placedTrigger.X, placedTrigger.Y, 0, ct);

            if (placed is null || !await ConfigureAsync(ctx, placed.ObjectId, box, ct))
                return false;
        }

        return true;
    }

    private static Task<bool> ConfigureAsync(
        BotContext ctx,
        int objectId,
        Box box,
        CancellationToken ct
    ) =>
        WiredActivities.ConfigureAsync(
            ctx,
            objectId,
            box.Offer.Definition.Logic,
            box.Ints,
            box.Picks,
            ct,
            box.Text,
            box.UserSources
        );

    /// <summary>Starts a round on the game timer and checks it starts and ends for the host.</summary>
    private static async Task RunRoundAsync(BotContext ctx, GameRoom game, CancellationToken ct)
    {
        var self = ctx.Room?.SelfObjectId ?? -1;

        var started = ctx.Client.ExpectChatAsync(
            line => line.ObjectId == self && line.Text == game.StartText,
            TimeSpan.FromSeconds(10),
            "game start message"
        );
        var ended = ctx.Client.ExpectChatAsync(
            line => line.ObjectId == self && line.Text == game.EndText,
            ROUND_TIMEOUT,
            "game end message"
        );

        await ctx.Client.UseAsync(game.CounterObjectId, 0, ct);

        ctx.Metrics.Check(
            "game.round.starts",
            await CompletesAsync(started, ct),
            CheckSeverity.Hard,
            $"{ctx.Client.Name}: room {game.RoomId}"
        );

        // The host plays along while the round runs.
        var deadline = DateTime.UtcNow + ROUND_TIMEOUT;

        while (!ended.IsCompleted && DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (ctx.RandomWalkableTile() is { } tile)
                await ctx.Client.WalkToAsync(tile.X, tile.Y, ct);

            await ctx.ThinkAsync(ct);
        }

        ctx.Metrics.Check(
            "game.round.ends",
            await CompletesAsync(ended, ct),
            CheckSeverity.Hard,
            $"{ctx.Client.Name}: room {game.RoomId}"
        );
    }

    /// <summary>
    /// Plays a hosted game: enters, steps on pads and checks the wired answers each step.
    /// </summary>
    public static async Task PlayAsync(BotContext ctx, CancellationToken ct)
    {
        if (
            ctx.World.RandomGame(ctx.Random) is not { } game
            || !await ctx.EnterAsync(game.RoomId, ct)
        )
            return;

        var steps = ctx.Random.Next(3, 8);

        for (var i = 0; i < steps && !ct.IsCancellationRequested; i++)
        {
            var room = ctx.Room;

            if (room is null || room.RoomId != game.RoomId)
                return;

            var self = room.SelfObjectId;
            // A pad someone stands on refuses the walk; a player waits for a free one.
            var freePads = game.Pads.Where(x => !room.IsTakenByOther(x.X, x.Y, self)).ToList();

            if (freePads.Count == 0)
            {
                await ctx.ThinkAsync(ct);

                continue;
            }

            var pad = ctx.Pick(freePads);

            // Already on it: step off first, or walking there triggers nothing.
            if (room.Position(self) == pad && ctx.RandomWalkableTile() is { } off)
                await ctx.Client.WalkToAsync(off.X, off.Y, ct);

            var scored = ctx.Client.ExpectChatAsync(
                line => line.ObjectId == self && line.Text == game.GoalText,
                TimeSpan.FromSeconds(4) + ctx.Options.ReplyTimeout,
                "goal message"
            );

            if (!await ctx.Client.WalkToAsync(pad.X, pad.Y, ct))
            {
                // Someone else stood on it or blocked the way; not the server's fault.
                Observe(scored);
                continue;
            }

            ctx.Metrics.Check(
                "game.pad.answers_the_player",
                await CompletesAsync(scored, ct),
                CheckSeverity.Hard,
                $"{ctx.Client.Name}: room {game.RoomId} pad ({pad.X},{pad.Y})"
            );

            await ctx.ThinkAsync(ct);
        }
    }

    private static async Task<bool> CompletesAsync(Task task, CancellationToken ct)
    {
        try
        {
            await task.WaitAsync(ct);

            return true;
        }
        catch (Client.BotTimeoutException)
        {
            return false;
        }
    }

    private static void Observe(Task task) =>
        _ = task.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default
        );

    private sealed record Box(
        FurniOffer Offer,
        IReadOnlyList<int> Ints,
        IReadOnlyList<int> Picks,
        string? Text,
        IReadOnlyList<int>? UserSources
    );
}
