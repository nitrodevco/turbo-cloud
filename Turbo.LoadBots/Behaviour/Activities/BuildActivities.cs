using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Turbo.LoadBots.Metrics;

namespace Turbo.LoadBots.Behaviour.Activities;

/// <summary>Furnishing rooms: buying furni, placing it, moving it about, tidying up.</summary>
public static class BuildActivities
{
    /// <summary>
    /// A few furni into a room of the bot's own: buy (or take from the inventory), place,
    /// and show the room off with a line of chat.
    /// </summary>
    public static async Task DecorateAsync(
        BotContext ctx,
        int minItems,
        int maxItems,
        CancellationToken ct
    )
    {
        var purpose =
            ctx.Persona is Persona.Architect ? RoomPurpose.Architecture : RoomPurpose.Decor;

        if (
            await ctx.OwnRoomAsync(purpose, ct) is not { } roomId
            || !await ctx.EnterAsync(roomId, ct)
        )
            return;

        if (ctx.Knowledge.DecorOffers.Count == 0)
            return;

        var room = ctx.Room;

        if (room is null)
            return;

        if (room.Items().Count >= ctx.Options.Run.MaxItemsPerRoom)
        {
            await TidyAsync(ctx, ct);

            return;
        }

        var count = ctx.Random.Next(minItems, maxItems + 1);

        for (var i = 0; i < count && !ct.IsCancellationRequested; i++)
        {
            await ctx.PlaceSomewhereAsync(ctx.Pick(ctx.Knowledge.DecorOffers), ct);
            await Task.Delay(ctx.Random.Next(100, 600), ct);
        }

        ctx.World.Publish(new PublicRoom(roomId, ctx.Client.Name, purpose));

        await ctx.Client.SayAsync("Come and see my room!", ct);
    }

    /// <summary>Moves and turns some of the furni already in the bot's current room.</summary>
    public static async Task RearrangeAsync(BotContext ctx, CancellationToken ct)
    {
        var room = ctx.Room;

        if (room is null || !room.IsOwner)
            return;

        var mine = room.Items().Where(x => x.OwnerId == ctx.Client.PlayerId).ToList();

        for (var i = 0; i < Math.Min(5, mine.Count); i++)
        {
            var item = ctx.Pick(mine);
            var definition = ctx.Knowledge.FloorDefinition(item.SpriteId);

            if (definition is null || definition.IsWired)
                continue;

            var rotation = ctx.Pick<int>([0, 2, 4, 6]);
            var (width, length) = Client.RoomView.Footprint(definition, rotation);
            var spots = room.FreeSpots(width, length, ctx.Knowledge, ctx.Door(room.RoomId));

            if (spots.Count == 0)
                continue;

            var (x, y) = ctx.Pick(spots);

            await ctx.Client.MoveAsync(item.ObjectId, x, y, rotation, ct);
        }
    }

    /// <summary>Picks up some of the bot's furni so a long run does not fill its rooms.</summary>
    public static async Task TidyAsync(BotContext ctx, CancellationToken ct)
    {
        var room = ctx.Room;

        if (room is null || !room.IsOwner)
            return;

        var mine = room.Items().Where(x => x.OwnerId == ctx.Client.PlayerId).ToList();
        var toPick = mine.OrderBy(_ => ctx.Random.Next()).Take(Math.Max(1, mine.Count / 3));

        foreach (var item in toPick)
        {
            if (!await ctx.Client.PickupAsync(item.ObjectId, ct))
                continue;

            await Task.Delay(ctx.Random.Next(50, 300), ct);
        }
    }

    /// <summary>
    /// Draws a new floor plan for a room of the bot's own: a few overlapping rectangles at one
    /// or two heights, with the door on the left edge. Then checks the room comes back drawn
    /// that way and furnishes it.
    /// </summary>
    public static async Task DrawFloorPlanAsync(BotContext ctx, CancellationToken ct)
    {
        if (
            await ctx.OwnRoomAsync(RoomPurpose.Architecture, ct) is not { } roomId
            || !await ctx.EnterAsync(roomId, ct)
        )
            return;

        // A redraw sends everyone's furni in the way home; start from an empty room.
        await TidyAllAsync(ctx, ct);

        var (model, doorX, doorY) = FloorPlanGenerator.Generate(ctx.Random);

        var saved = await ctx.Client.SaveFloorPlanAsync(model, doorX, doorY, 2, ct);

        ctx.Metrics.Check(
            "room.floorplan.redraws_room",
            saved,
            CheckSeverity.Hard,
            $"{ctx.Client.Name}: room {roomId}"
        );

        if (!saved)
            return;

        // The redraw reloads the room for everyone in it; enter again to see it fresh.
        await Task.Delay(500, ct);
        await ctx.Client.EnterRoomAsync(roomId, ct);

        var map = ctx.Room?.Map;
        var rows = model.Split('\r');

        ctx.Metrics.Check(
            "room.floorplan.survives_reentry",
            map is not null && map.Length == rows.Length && map.Width == rows[0].Length,
            CheckSeverity.Hard,
            $"{ctx.Client.Name}: room {roomId} drew {rows[0].Length}x{rows.Length}, entered {map?.Width}x{map?.Length}"
        );

        await DecorateAsync(ctx, 4, 12, ct);
    }

    private static async Task TidyAllAsync(BotContext ctx, CancellationToken ct)
    {
        var room = ctx.Room;

        if (room is null)
            return;

        foreach (var item in room.Items().Where(x => x.OwnerId == ctx.Client.PlayerId))
            await ctx.Client.PickupAsync(item.ObjectId, ct);
    }
}

/// <summary>Random floor plans in the editor's model format.</summary>
public static class FloorPlanGenerator
{
    private const char CLOSED = 'x';

    /// <summary>
    /// A plan of 8–24 tiles a side: one main rectangle and up to three more overlapping it, one
    /// of them raised a step. The door is on the left edge, on a row of the main rectangle.
    /// </summary>
    public static (string Model, int DoorX, int DoorY) Generate(Random random)
    {
        var width = random.Next(8, 25);
        var length = random.Next(8, 25);
        var grid = new char[length, width];

        for (var y = 0; y < length; y++)
        for (var x = 0; x < width; x++)
            grid[y, x] = CLOSED;

        // The main floor, with its left column at x = 1 so the door can sit at x = 0.
        var mainRight = random.Next(Math.Max(4, width / 2), width);
        var mainTop = random.Next(0, length / 3);
        var mainBottom = random.Next(Math.Max(mainTop + 3, length * 2 / 3), length);

        Fill(grid, 1, mainTop, mainRight, mainBottom, '0');

        var extras = random.Next(0, 4);

        for (var i = 0; i < extras; i++)
        {
            // Each extra touches the main floor so the plan stays one connected room.
            var x1 = random.Next(1, mainRight);
            var y1 = random.Next(0, length - 2);
            var x2 = Math.Min(width - 1, x1 + random.Next(2, 8));
            var y2 = Math.Min(length - 1, y1 + random.Next(2, 8));
            var height = random.Next(2) == 0 ? '0' : '1';

            Fill(grid, x1, y1, x2, y2, height);
        }

        var doorY = random.Next(mainTop, mainBottom + 1);
        grid[doorY, 0] = '0';
        grid[doorY, 1] = '0';

        var model = new StringBuilder();

        for (var y = 0; y < length; y++)
        {
            if (y > 0)
                model.Append('\r');

            for (var x = 0; x < width; x++)
                model.Append(grid[y, x]);
        }

        return (model.ToString(), 0, doorY);
    }

    private static void Fill(char[,] grid, int x1, int y1, int x2, int y2, char height)
    {
        for (var y = y1; y <= y2; y++)
        for (var x = x1; x <= x2; x++)
            grid[y, x] = height;
    }
}
