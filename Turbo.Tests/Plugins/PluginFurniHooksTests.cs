using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Plugins;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The furni hooks a plugin's logic has beyond use and walk-on (docs/plugin-wired.md, "Furni
/// logic"): walking a user to one of its tiles and acting on arrival, the user actions (stop, turn,
/// one step shown), forwarding a player with an entry extension, its object extra, its extra data
/// and pair, and messages from the plugin's own handlers through the room grain. The plugin is
/// compiled against the contracts and run in a live room, as a hotel runs it.
/// </summary>
public sealed class PluginFurniHooksTests : IDisposable
{
    private const int ALICE = 5;
    private const int ALICE_PLAYER = 100 + ALICE;
    private const int PORTAL = 10;
    private const int PARTNER = 11;
    private const int CHAIR = 12;
    private static readonly RoomId FAR_ROOM = new(77);

    private readonly WiredRoom _room = new(10, 10);

    // The plugin's registrations, the linked pair among them, go when the test ends, as when the
    // plugin unloads.
    private IDisposable? _plugin;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _plugin?.Dispose();

    /// <summary>
    /// A portal: used from across the room, it walks the user to the nearest of its seam tiles;
    /// on arrival it shows the user stepping through the wall and forwards them to the far room
    /// with its own data for the far room's arrival policies, as a plain entry.
    /// </summary>
    [Fact]
    public async Task A_walk_to_the_nearest_tile_runs_the_arrival_once_there_and_the_portal_forwards_the_player()
    {
        var alice = _room.Enter(ALICE, 2, 7, "alice");
        var portal = await PlacePortalAsync();

        (
            await _room
                .Harness.Module<RoomActionModule>()
                .UseItemByIdAsync(_room.ContextFor(ALICE), portal.ObjectId, Ct, param: 0)
        )
            .Should()
            .BeTrue();

        Forwards().Should().BeEmpty("nobody has arrived yet");

        await TickAsync(12);

        (alice.X, alice.Y).Should().Be((2, 3), "(2, 3) is nearer than (6, 2)");
        var forward = Forwards().Should().ContainSingle("the arrival ran once").Subject;
        forward.Args[0].Should().Be(FAR_ROOM);
        forward
            .Args[1]
            .Should()
            .BeEquivalentTo(
                new RoomEntrySnapshot
                {
                    Method = RoomEntryMethodType.Default,
                    TeleportId = 0,
                    SourceRoomId = _room.Harness.Room.RoomId,
                    Extension = new RoomEntryExtension("portals", $$"""{"from":{{PORTAL}}}"""),
                },
                "a plain entry carrying the portal's data, never a teleport's"
            );
        _room
            .Harness.Fakes.AvatarUpdates()
            .SelectMany(x => x)
            .Should()
            .Contain(
                x => x.ObjectId.Value == ALICE && x.Status.Contains("mv -1,3,"),
                "the step through the wall to the west is shown"
            );
        alice.Rotation.Should().Be(Rotation.West);
        (alice.X, alice.Y).Should().Be((2, 3), "a shown step does not move the user");

        await TickAsync(4);

        alice.HasStatus(AvatarStatusType.Move).Should().BeFalse("the shown step is shown once");
    }

    [Fact]
    public async Task A_walk_to_tiles_the_user_cannot_reach_answers_false_and_never_arrives()
    {
        _room.Enter(ALICE, 2, 7, "alice");
        await PlacePortalAsync();

        (await InvokeAsync(PORTAL, "walk_nowhere")).Payload.Should().Be("false");

        await TickAsync(8);

        Forwards().Should().BeEmpty();
    }

    [Fact]
    public async Task A_message_reaches_the_plugins_logic_which_sets_the_object_extra_and_reads_its_pair()
    {
        _room.Enter(ALICE, 2, 7, "alice");
        var portal = await PlacePortalAsync();
        portal.ExtraData.UpdateSection(
            RoomLinkerData.SECTION,
            new RoomLinkerData { ItemId = PARTNER }
        );

        var reply = await InvokeAsync(PORTAL, "extra", 4242);

        reply.Status.Should().Be(FurniLogicReplyStatus.Handled);
        reply.Read<Answer>().Should().Be(new Answer(PARTNER, "alice"));
        _room
            .Harness.Fakes.SentToRoom<ObjectUpdateMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.FloorItem.Extra.Should()
            .Be(4242, "the client is shown the new extra");
    }

    [Theory]
    [InlineData(99, "extra", FurniLogicReplyStatus.UnknownItem)]
    [InlineData(CHAIR, "extra", FurniLogicReplyStatus.WrongLogic)]
    [InlineData(PORTAL, "throw", FurniLogicReplyStatus.Failed)]
    [InlineData(PORTAL, "unknown", FurniLogicReplyStatus.NotHandled)]
    public async Task A_message_that_reaches_no_logic_or_fails_is_answered_and_never_thrown(
        int itemId,
        string command,
        FurniLogicReplyStatus status
    )
    {
        _room.Enter(ALICE, 2, 7, "alice");
        await PlacePortalAsync();
        _room.AddFloorItem(CHAIR, 7, 7);

        (await InvokeAsync(itemId, command)).Status.Should().Be(status);
    }

    [Fact]
    public async Task The_logic_stops_a_walking_user_where_it_stands_and_turns_it()
    {
        var alice = _room.Enter(ALICE, 1, 8, "alice");
        await PlacePortalAsync();
        await _room.Harness.Module<RoomAvatarModule>().WalkAvatarToAsync(alice, 8, 8, Ct);
        await TickAsync(3);

        (await InvokeAsync(PORTAL, "stop")).IsHandled.Should().BeTrue();
        var stoppedAt = (alice.X, alice.Y);
        (await InvokeAsync(PORTAL, "turn", (int)Rotation.South)).IsHandled.Should().BeTrue();
        await TickAsync(3);

        alice.IsWalking.Should().BeFalse();
        (alice.X, alice.Y).Should().Be(stoppedAt).And.NotBe((8, 8));
        _room
            .Harness.Fakes.AvatarUpdates()
            .Last()
            .Single(x => x.ObjectId.Value == ALICE)
            .BodyRotation.Should()
            .Be(Rotation.South);
    }

    /// <summary>
    /// A linked pair's half forwarding a player to the room its other half stands in passes that
    /// room's door as a teleporter does (RoomEntryPreparationTests checks the door itself): core
    /// finds the partner's room, the plugin never says so. A forward to any other room, or from a
    /// logic that is no linked pair, is a plain entry. Either way the entry method stays a plain
    /// one, so the far room's arrival policies still place them.
    /// </summary>
    [Theory]
    [InlineData("plugin_portal", 77, true)]
    [InlineData("plugin_portal", 78, false)]
    [InlineData("plugin_door", 77, false)]
    public async Task Only_a_linked_pair_forwarding_to_its_partners_room_passes_the_door(
        string logicKey,
        int partnerRoom,
        bool passesDoor
    )
    {
        var db = new InMemoryDb();

        await using (var dbCtx = await db.CreateDbContextAsync(Ct))
        {
            dbCtx.Furnitures.Add(
                new Turbo.Database.Entities.Furniture.FurnitureEntity
                {
                    Id = PARTNER,
                    PlayerEntityId = ALICE_PLAYER,
                    FurnitureDefinitionEntityId = 1,
                    RoomEntityId = partnerRoom,
                }
            );
            await dbCtx.SaveChangesAsync(Ct);
        }

        _room.Harness.Room.UseDatabase(db);
        _room.Enter(ALICE, 2, 7, "alice");
        var portal = await PlacePortalAsync(logicKey);
        portal.ExtraData.UpdateSection(
            RoomLinkerData.SECTION,
            new RoomLinkerData { ItemId = PARTNER }
        );

        (await InvokeAsync(PORTAL, "forward", FAR_ROOM.Value, logicKey))
            .IsHandled.Should()
            .BeTrue();

        var entry = (RoomEntrySnapshot)Forwards().Should().ContainSingle().Subject.Args[1]!;
        entry.ThroughLinkedPair.Should().Be(passesDoor);
        entry.PassesDoor.Should().Be(passesDoor);
        entry.Method.Should().Be(RoomEntryMethodType.Default, "never a teleporter's arrival");
    }

    [Fact]
    public async Task A_linked_pair_logic_is_sold_as_a_pair_while_its_plugin_is_loaded()
    {
        var registration = await new PluginLogicFeatureProcessor(
            _room.Harness.LogicProvider
        ).ProcessAsync(Plugin(), _room.Harness.Services, Ct);

        TeleportFurniture.IsLinkedPair("plugin_portal", "portal_sprite").Should().BeTrue();

        registration.Dispose();

        TeleportFurniture.IsLinkedPair("plugin_portal", "portal_sprite").Should().BeFalse();
    }

    private sealed record Answer(int Paired, string? Sender);

    private Task<FurniLogicReply> InvokeAsync(
        int itemId,
        string command,
        int value = 0,
        string logicKey = "plugin_portal"
    ) =>
        _room.Harness.Room.InvokeFurniLogicAsync(
            itemId,
            FurniLogicMessage.Of(logicKey, new { Do = command, Value = value }, ALICE_PLAYER),
            Ct
        );

    private IEnumerable<FakeCall> Forwards() => _room.Harness.Fakes.Log.Of("ForwardToRoomAsync");

    private async Task<IRoomFloorItem> PlacePortalAsync(string logicKey = "plugin_portal")
    {
        _plugin = await new PluginLogicFeatureProcessor(_room.Harness.LogicProvider).ProcessAsync(
            Plugin(),
            _room.Harness.Services,
            Ct
        );

        return _room.AddFloorItem(
            PORTAL,
            2,
            2,
            logicKey,
            canWalk: false,
            createLogic: (_, ctx) => _room.Harness.LogicProvider.CreateLogicInstance(logicKey, ctx)
        );
    }

    private async Task TickAsync(int ticks)
    {
        var tickMs = 500;

        for (var i = 0; i < ticks; i++)
        {
            _room.Clock.AdvanceMs(tickMs);

            var now = _room.Harness.NowMs();

            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(now, Ct);
            await _room.Harness.Module<RoomTimerSystem>().ProcessTimersAsync(now, Ct);
        }
    }

    private static Assembly Plugin() =>
        Assembly.Load(PluginBuilder.Compile("portals", [PORTAL_SOURCE]));

    private const string PORTAL_SOURCE = """"
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Turbo.Contracts.Furni;
        using Turbo.Contracts.Rooms;
        using Turbo.Primitives.Furniture;
        using Turbo.Primitives.Rooms;
        using Turbo.Primitives.Rooms.Enums;
        using Turbo.Primitives.Rooms.Snapshots;

        namespace Portals;

        public sealed record Command(string Do, int Value);

        [FurniLogic("plugin_portal", LinkedPair = true)]
        public sealed class Portal : PortalBase;

        // The same furni, but no linked pair.
        [FurniLogic("plugin_door")]
        public sealed class Door : PortalBase;

        public abstract class PortalBase : FurniLogic
        {
            public override async Task<bool> OnUseAsync(IFurniContext ctx, RoomUser? user, int param, CancellationToken ct)
            {
                if (user is null)
                    return false;

                RoomTilePoint[] seam = [new(ctx.Self.X + 4, ctx.Self.Y), new(ctx.Self.X, ctx.Self.Y + 1)];

                await ctx.WalkToAsync(user, seam, async (arrived, token) =>
                {
                    await ctx.ShowStepAsync(arrived, new RoomTilePoint(-1, arrived.Y), token);
                    await ctx.ForwardToRoomAsync(
                        arrived,
                        new RoomId(77),
                        new RoomEntryExtension("portals", $$"""{"from":{{ctx.Self.Id}}}"""),
                        token
                    );
                }, ct);

                return true;
            }

            public override async Task<FurniLogicReply> OnMessageAsync(
                IFurniContext ctx, FurniLogicMessage message, RoomUser? sender, CancellationToken ct)
            {
                var command = message.Read<Command>();

                switch (command?.Do)
                {
                    case "extra":
                        await ctx.SetObjectExtraAsync(command.Value, ct);
                        return FurniLogicReply.Handled(new { Paired = ctx.PairedItemId, Sender = sender?.Name });
                    case "stop":
                        return await ctx.StopWalkingAsync(sender!, ct) ? FurniLogicReply.Handled() : FurniLogicReply.NotHandled;
                    case "turn":
                        return await ctx.SetRotationAsync(sender!, (Rotation)command.Value, ct) ? FurniLogicReply.Handled() : FurniLogicReply.NotHandled;
                    case "forward":
                        return await ctx.ForwardToRoomAsync(sender!, new RoomId(command.Value), null, ct) ? FurniLogicReply.Handled() : FurniLogicReply.NotHandled;
                    case "walk_nowhere":
                        var walking = await ctx.WalkToAsync(sender!, [new(-5, -5), new(40, 40)], (_, _) => throw new InvalidOperationException("never"), ct);
                        return FurniLogicReply.Handled(walking ? "true" : "false");
                    case "throw":
                        throw new InvalidOperationException("a plugin's bug");
                    default:
                        return FurniLogicReply.NotHandled;
                }
            }
        }
        """";
}
