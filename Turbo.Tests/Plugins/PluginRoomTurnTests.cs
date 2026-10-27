using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Plugins;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// What a plugin's furni logic does in its room's turn beyond walks and forwards
/// (docs/plugin-wired.md, "Furni logic"): a run scheduled by the room's clock, who may use and
/// walk on it, walks in the order it gives, the tiles around it, lines said over a user, effects
/// and temporary furni. The plugin is compiled against the contracts and run in a live room, as a
/// hotel runs it; these are what the ported plugins (a slot machine, skill furni, a game arena)
/// were built on core's own logic for before.
/// </summary>
public sealed class PluginRoomTurnTests : IDisposable
{
    private const int ALICE = 5;
    private const int ALICE_PLAYER = 100 + ALICE;
    private const int MACHINE = 10;
    private const int CRATE = 11;
    private const int SEAT = 12;
    private const string LOGIC = "plugin_machine";

    private readonly WiredRoom _room = new(10, 10);

    private IDisposable? _plugin;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _plugin?.Dispose();

    [Fact]
    public async Task A_scheduled_run_runs_once_at_its_time_by_the_rooms_clock()
    {
        _room.Enter(ALICE, 5, 5, "alice");
        var machine = await PlaceMachineAsync();

        (await InvokeAsync("schedule", 1200)).IsHandled.Should().BeTrue();

        await AdvanceAsync(1100);
        machine.Logic.GetState().Should().Be(0, "it is not due yet");

        await AdvanceAsync(100);
        machine.Logic.GetState().Should().Be(1, "due at 1200 ms, not at the next half-second tick");

        await machine.Logic.SetStateAsync(0);
        await AdvanceAsync(3000);
        machine.Logic.GetState().Should().Be(0, "it runs once");
    }

    [Fact]
    public async Task A_picked_up_furni_lets_go_of_its_logic_and_its_scheduled_run_never_runs()
    {
        _room.Enter(ALICE, 5, 5, "alice");
        var machine = await PlaceMachineAsync();
        await InvokeAsync("schedule", 1000);

        await _room
            .Harness.Module<RoomObjectModule>()
            .RemoveObjectAsync(ActionContext.CreateForPlayer(ALICE_PLAYER, 1), machine, Ct);
        await AdvanceAsync(2000);

        machine.Logic.GetState().Should().Be(3, "detached (3), never the scheduled 1");
    }

    /// <summary>
    /// The definition says nobody may use it and nobody may walk on it; the logic says everybody
    /// and yes, as a slot machine every visitor plays and a skill tile do.
    /// </summary>
    [Fact]
    public async Task The_logic_decides_who_may_use_it_and_whether_it_is_walked_on()
    {
        _room.Enter(ALICE, 5, 5, "alice");
        var machine = await PlaceMachineAsync(usagePolicy: FurnitureUsageType.Nobody);

        machine.Logic.GetUsagePolicy().Should().Be(FurnitureUsageType.Everybody);
        ((IFurnitureFloorLogic)machine.Logic).CanWalk().Should().BeTrue();
        (
            await _room
                .Harness.Module<RoomActionModule>()
                .UseItemByIdAsync(_room.ContextFor(ALICE), MACHINE, Ct, param: 0)
        )
            .Should()
            .BeTrue("a visitor without rights may use it");
    }

    /// <summary>
    /// Tiles in the order the logic gives, best first: the avatar is sent to the first it can
    /// reach though another is nearer (a machine used from in front of it), and the arrival runs
    /// there.
    /// </summary>
    [Fact]
    public async Task A_walk_in_order_goes_to_the_first_reachable_tile_not_the_nearest()
    {
        var alice = _room.Enter(ALICE, 2, 7, "alice");
        var machine = await PlaceMachineAsync();

        (
            await _room
                .Harness.Module<RoomActionModule>()
                .UseItemByIdAsync(_room.ContextFor(ALICE), MACHINE, Ct, param: 0)
        )
            .Should()
            .BeTrue();
        await TickAsync(16);

        (alice.X, alice.Y).Should().Be((7, 7), "(7, 7) comes first though (2, 3) is nearer");
        machine.Logic.GetState().Should().Be(2, "the arrival ran there");
    }

    /// <summary>
    /// The tile in front (the way it faces, north) and the tiles touching it a user may stand on,
    /// nearest to the user first (the one it stands on first of all), never the one under the
    /// crate nobody may stand on. The seat is the top furni of its tile.
    /// </summary>
    [Fact]
    public async Task The_logic_reads_the_tiles_around_it_and_what_stands_on_them()
    {
        _room.Enter(ALICE, 3, 2, "alice");
        _room.AddFloorItem(CRATE, 1, 1, canWalk: false);
        _room.AddFloorItem(SEAT, 2, 3, canSit: true);
        await PlaceMachineAsync();

        var reply = (await InvokeAsync("tiles")).Read<Tiles>()!;

        reply.Front.Should().Be("2,1");
        reply
            .Beside.Should()
            .Equal(
                ["3,2", "3,1", "3,3", "2,1", "2,3", "1,2", "1,3"],
                "nearest to alice at (3, 2) first"
            );
        reply.Seats.Should().Equal(["2,3"]);
        reply.FreeCrate.Should().BeFalse("a furni stands there");
        reply.FreeAlice.Should().BeFalse("someone stands there");
        reply.FreeEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task A_whisper_over_a_player_is_shown_to_them_alone_in_the_bubble_it_names()
    {
        _room.Enter(ALICE, 5, 5, "alice");
        await PlaceMachineAsync();

        (await InvokeAsync("whisper", 202)).IsHandled.Should().BeTrue();

        var whisper = _room
            .Harness.Fakes.SentToPlayer<WhisperMessageComposer>(ALICE_PLAYER)
            .Should()
            .ContainSingle()
            .Subject;
        whisper.ObjectId.Value.Should().Be(ALICE);
        whisper.Text.Should().Be("hello alice");
        whisper.StyleId.Should().Be(202);
        whisper.ChatBubbleWidthOverride.Should().Be(1);
        _room.Harness.Fakes.SentToRoom<WhisperMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task The_logic_puts_an_effect_on_a_user_and_takes_it_off()
    {
        var alice = _room.Enter(ALICE, 5, 5, "alice");
        await PlaceMachineAsync();

        (await InvokeAsync("effect", 96)).IsHandled.Should().BeTrue();
        alice.EffectId.Should().Be(96);

        (await InvokeAsync("effect", 0)).IsHandled.Should().BeTrue();
        alice.EffectId.Should().Be(0);
    }

    /// <summary>
    /// A coin dropped by a game: a temporary furni of the machine's kind running another logic
    /// (the definition changed with <c>with</c>), made in the temporary band for the machine's
    /// owner. Only a temporary furni is taken out again.
    /// </summary>
    [Fact]
    public async Task A_temporary_furni_is_made_with_the_logic_its_definition_names()
    {
        _room.Enter(ALICE, 5, 5, "alice");
        await PlaceMachineAsync();
        // The room's item factory, as the server's makes a furni of the definition it is given.
        _room.Harness.Fakes.Handlers["CreateFloorItem"] = call =>
        {
            var item = new RoomFloorItem
            {
                ObjectId = (RoomObjectId)call.Args[0]!,
                OwnerId = (PlayerId)call.Args[1]!,
                OwnerName = "test",
                Definition = (FurnitureDefinitionSnapshot)call.Args[2]!,
            };

            item.SetExtraData(null);
            item.SetLogic(
                new FurnitureFloorLogic(
                    _room.StuffData,
                    new RoomFloorItemContext(_room.Harness.Room, item)
                )
            );

            return item;
        };

        var dropped = (await InvokeAsync("drop", 6)).Read<int>();
        var made = _room.Harness.Fakes.Log.Of("CreateFloorItem").Should().ContainSingle().Subject;

        FurniIdBands.IsTemporary(dropped).Should().BeTrue();
        ((PlayerId)made.Args[1]!).Value.Should().Be(1, "the machine's owner");
        ((FurnitureDefinitionSnapshot)made.Args[2]!).LogicName.Should().Be("plugin_coin");
        _room.Harness.Module<RoomFurniModule>().TryGetItem(dropped, out var coin).Should().BeTrue();
        (((IRoomFloorItem)coin!).X, ((IRoomFloorItem)coin).Y).Should().Be((6, 6));

        (await InvokeAsync("remove", dropped)).IsHandled.Should().BeTrue();
        _room.Harness.Module<RoomFurniModule>().TryGetItem(dropped, out _).Should().BeFalse();
        (await InvokeAsync("remove", MACHINE)).IsHandled.Should().BeFalse("only a temporary one");
    }

    private sealed record Tiles(
        string? Front,
        string[] Beside,
        string[] Seats,
        bool FreeCrate,
        bool FreeAlice,
        bool FreeEmpty
    );

    private Task<FurniLogicReply> InvokeAsync(string command, int value = 0) =>
        _room.Harness.Room.InvokeFurniLogicAsync(
            MACHINE,
            FurniLogicMessage.Of(LOGIC, new { Do = command, Value = value }, ALICE_PLAYER),
            Ct
        );

    private async Task<IRoomFloorItem> PlaceMachineAsync(
        FurnitureUsageType usagePolicy = FurnitureUsageType.Everybody
    )
    {
        _plugin = await new PluginLogicFeatureProcessor(_room.Harness.LogicProvider).ProcessAsync(
            Plugin(),
            _room.Harness.Services,
            Ct
        );

        return _room.AddFloorItem(
            MACHINE,
            2,
            2,
            LOGIC,
            canWalk: false,
            usagePolicy: usagePolicy,
            totalStates: 4,
            createLogic: (_, ctx) => _room.Harness.LogicProvider.CreateLogicInstance(LOGIC, ctx)
        );
    }

    /// <summary>Moves the room's clock on and runs its timers, as its loop does.</summary>
    private async Task AdvanceAsync(int ms)
    {
        for (var left = ms; left > 0; left -= 100)
        {
            _room.Clock.AdvanceMs(Math.Min(100, left));
            await _room
                .Harness.Module<RoomTimerSystem>()
                .ProcessTimersAsync(_room.Harness.NowMs(), Ct);
        }
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _room.Clock.AdvanceMs(500);

            var now = _room.Harness.NowMs();

            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(now, Ct);
            await _room.Harness.Module<RoomTimerSystem>().ProcessTimersAsync(now, Ct);
        }
    }

    private static Assembly Plugin() =>
        Assembly.Load(PluginBuilder.Compile("machines", [MACHINE_SOURCE]));

    private const string MACHINE_SOURCE = """"
        using System;
        using System.Linq;
        using System.Threading;
        using System.Threading.Tasks;
        using Turbo.Contracts.Furni;
        using Turbo.Contracts.Rooms;
        using Turbo.Primitives.Furniture;
        using Turbo.Primitives.Rooms;
        using Turbo.Primitives.Rooms.Enums;

        namespace Machines;

        public sealed record Command(string Do, int Value);

        [FurniLogic("plugin_machine")]
        public sealed class Machine : FurniLogic
        {
            public override FurnitureUsageType? UsagePolicy => FurnitureUsageType.Everybody;

            public override bool? Walkable => true;

            public override Task OnDetachedAsync(IFurniContext ctx, CancellationToken ct) =>
                ctx.SetStateAsync(3, ct);

            public override async Task<bool> OnUseAsync(IFurniContext ctx, RoomUser? user, int param, CancellationToken ct)
            {
                if (user is null)
                    return false;

                await ctx.WalkToInOrderAsync(user, [new(7, 7), new(2, 3)], (_, token) => ctx.SetStateAsync(2, token), ct);

                return true;
            }

            public override async Task<FurniLogicReply> OnMessageAsync(
                IFurniContext ctx, FurniLogicMessage message, RoomUser? sender, CancellationToken ct)
            {
                var command = message.Read<Command>()!;
                static string Text(RoomTilePoint tile) => $"{tile.X},{tile.Y}";

                switch (command.Do)
                {
                    case "schedule":
                        ctx.Schedule(TimeSpan.FromMilliseconds(command.Value), token => ctx.SetStateAsync(1, token));
                        return FurniLogicReply.Handled();
                    case "tiles":
                        var beside = ctx.TilesBeside(sender!);
                        return FurniLogicReply.Handled(new
                        {
                            Front = ctx.TileInFront is { } front ? Text(front) : null,
                            Beside = beside.Select(Text).ToArray(),
                            Seats = beside.Where(t => ctx.Map.FurniAt(t) is [.., { Definition.CanSit: true }]).Select(Text).ToArray(),
                            FreeCrate = ctx.Map.IsFree(new(1, 1)),
                            FreeAlice = ctx.Map.IsFree(new(sender!.X, sender.Y)),
                            FreeEmpty = ctx.Map.IsFree(new(8, 8)),
                        });
                    case "whisper":
                        var said = await ctx.SayAsync(sender!, "hello " + sender!.Name,
                            new RoomSpeech { OnlyForThem = true, StyleId = command.Value, BubbleWidth = 1 }, ct);
                        return said ? FurniLogicReply.Handled() : FurniLogicReply.NotHandled;
                    case "effect":
                        return await ctx.SetEffectAsync(sender!, command.Value, ct) ? FurniLogicReply.Handled() : FurniLogicReply.NotHandled;
                    case "drop":
                        var coin = await ctx.PlaceTemporaryFurniAsync(
                            ctx.Self.Definition with { LogicName = "plugin_coin" },
                            new RoomTilePoint(command.Value, command.Value),
                            Rotation.North,
                            ct);
                        return FurniLogicReply.Handled(coin?.Id ?? 0);
                    case "remove":
                        return await ctx.RemoveTemporaryFurniAsync(command.Value, ct) ? FurniLogicReply.Handled() : FurniLogicReply.NotHandled;
                    default:
                        return FurniLogicReply.NotHandled;
                }
            }
        }
        """";
}
