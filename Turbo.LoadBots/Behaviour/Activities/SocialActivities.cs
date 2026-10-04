using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.LoadBots.Metrics;
using Turbo.Primitives.Navigator;

namespace Turbo.LoadBots.Behaviour.Activities;

/// <summary>Being a player among players: visiting rooms, walking about, talking.</summary>
public static class SocialActivities
{
    private static readonly string[] LINES =
    [
        "hey all",
        "nice room",
        "anyone up for a game?",
        "this place is busy",
        "love the furni",
        "brb",
        "where did you get that?",
        "o/",
    ];

    /// <summary>
    /// Visits someone else's room, found by word of mouth or through the navigator, and spends
    /// a while in it.
    /// </summary>
    public static async Task VisitAsync(BotContext ctx, CancellationToken ct)
    {
        int? roomId = null;

        if (ctx.Chance(0.5) && ctx.World.RandomRoom(ctx.Random, ctx.Client.Name) is { } known)
            roomId = known.RoomId;
        else
        {
            var code = ctx.Chance(0.5)
                ? NavigatorSearchCodes.POPULAR
                : NavigatorSearchCodes.HOTEL_VIEW;
            var listed = await ctx.Client.SearchRoomsAsync(code, string.Empty, ct);
            var candidates = listed
                ?.Where(x =>
                    x.OwnerId != ctx.Client.PlayerId && x.Users < x.MaxUsers && x.DoorMode == 0
                )
                .ToList();

            if (candidates is { Count: > 0 })
                roomId = ctx.Pick(candidates).RoomId;
            else if (ctx.World.RandomRoom(ctx.Random, ctx.Client.Name) is { } fallback)
                roomId = fallback.RoomId;
        }

        if (roomId is not { } id || !await ctx.EnterAsync(id, ct))
            return;

        // A room someone built should show its furni to a visitor.
        if (ctx.World.Room(id) is { Purpose: RoomPurpose.Decor or RoomPurpose.Architecture })
            ctx.Metrics.Check(
                "room.visit.sees_furni",
                ctx.Room?.Items().Count > 0,
                CheckSeverity.Soft,
                $"{ctx.Client.Name}: room {id}"
            );

        await HangOutAsync(ctx, ctx.Random.Next(2, 7), ct);
    }

    /// <summary>Walks about the current room, chatting and gesturing now and then.</summary>
    public static async Task HangOutAsync(BotContext ctx, int moves, CancellationToken ct)
    {
        for (var i = 0; i < moves && !ct.IsCancellationRequested && ctx.Room is not null; i++)
        {
            var roll = ctx.Random.NextDouble();

            if (roll < 0.55 && ctx.RandomWalkableTile() is { } tile)
                await ctx.Client.WalkToAsync(tile.X, tile.Y, ct);
            else if (roll < 0.75)
                await ctx.Client.SayAsync(ctx.Pick(LINES), ct);
            else if (roll < 0.8)
                await ctx.Client.ShoutAsync(ctx.Pick(LINES), ct);
            else if (roll < 0.87)
                await ctx.Client.DanceAsync(ctx.Random.Next(0, 5), ct);
            else if (roll < 0.94)
                await ctx.Client.ExpressAsync(1, ct); // wave
            else
                await ctx.Client.SignAsync(ctx.Random.Next(0, 15), ct);

            await ctx.ThinkAsync(ct);
        }
    }
}
