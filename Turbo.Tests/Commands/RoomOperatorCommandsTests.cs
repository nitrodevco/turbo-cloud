using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Commands;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// The operator commands that need the room itself: they run in its turn, and act on its live
/// state, so they are room commands even though staff, not the room's owner, use them.
/// </summary>
public class RoomOperatorCommandsTests
{
    private const int OWNER = 1;

    private readonly CommandRoomFixture _room = new();

    public RoomOperatorCommandsTests() =>
        _room.Commands.Register([
            new RoomKickAllCommand(),
            new RoomMuteCommand(),
            new RoomUnmuteCommand(),
            new UnloadRoomCommand(),
            new RoomEffectCommand(),
        ]);

    private bool IsMuted => (bool)RoomHarness.GetMember(_room.Harness.State, "IsRoomMuted")!;

    private int SessionsClosed => _room.Harness.Fakes.Log.Of("OnRemovedFromRoomAsync").Count();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BulkEvictionUsesNativeKickNoticesWithoutExtraTargetDialogs(bool unload)
    {
        _room.AddPlayer(
            OWNER,
            unload ? PermissionNodes.Command.UNLOADROOM : PermissionNodes.Command.ROOMKICKALL
        );
        _room.AddPlayer(3);
        _room.AddPlayer(4);

        await _room.SayAsync(OWNER, unload ? ":unloadroom" : ":roomkickall");

        _room
            .Harness.Fakes.Log.Of("OnRemovedFromRoomAsync")
            .Should()
            .HaveCount(unload ? 3 : 2)
            .And.OnlyContain(x => (bool)x.Args[1]!);
        _room
            .Harness.Fakes.Log.On<IPlayerNoticeService>()
            .Where(x => (PlayerId)x.Args[0]! != (PlayerId)OWNER)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task RoomKickAll_SendsOutEveryoneButTheOwnerStaffAndTheOneWhoAsked()
    {
        _room.AddPlayer(OWNER);
        _room.AddPlayer(2, PermissionNodes.Command.ROOMKICKALL); // asks, and may not moderate
        _room.AddPlayer(3);
        _room.AddPlayer(4);
        _room.AddPlayer(5, PermissionNodes.Room.MODERATE_ANY); // staff

        await _room.SayAsync(2, ":roomkickall");

        new[] { OWNER, 2, 3, 4, 5 }
            .Select(_room.IsInRoom)
            .Should()
            .Equal(true, true, false, false, true);
        SessionsClosed.Should().Be(2);
        _room.WhispersTo(2).Should().Equal("2 visitors were sent out of the room.");
        _room.Harness.Fakes.Log.On<IPlayerNoticeService>().Should().BeEmpty();
        _room.SaidToRoom().Should().BeEmpty();
    }

    [Fact]
    public async Task TheRoomTool_PutsTheDoorOnTheBell_RenamesTheRoom_AndClearsIt()
    {
        // The moderation tool's ModerateRoom (RoomToolCtrl.as:365): lock, change name, kick.
        _room.AddPlayer(OWNER);
        _room.AddPlayer(2);
        _room.AddPlayer(3);
        var db = (InMemoryDb)RoomHarness.GetField(_room.Harness.Room, "_dbCtxFactory")!;
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Rooms.Add(
                new Turbo.Database.Entities.Room.RoomEntity
                {
                    Id = 1,
                    Name = "Party",
                    PlayerEntityId = OWNER,
                    RoomModelEntityId = 1,
                    DoorMode = RoomDoorModeType.Open,
                    UsersNow = 0,
                    PlayersMax = 25,
                    WallHeight = -1,
                    HideWalls = false,
                    ThicknessWall = RoomThicknessType.Normal,
                    ThicknessFloor = RoomThicknessType.Normal,
                    AllowBlocking = false,
                    AllowPets = true,
                    AllowPetsEat = true,
                    TradeType = RoomTradeModeType.Disabled,
                    MuteType = ModSettingType.Owner,
                    KickType = ModSettingType.Owner,
                    BanType = ModSettingType.Owner,
                    ChatFloodType = ChatFloodSensitivityType.Minimal,
                    PlayerEntity = null!,
                    RoomModelEntity = null!,
                }
            );
            await ctx.SaveChangesAsync();
        }

        // The harness builds its snapshot without a row behind it; a loaded room has a password
        // and tags, even empty ones.
        var loaded = RoomHarness.GetMember(_room.Harness.State, "RoomSnapshot")!;
        RoomHarness.SetMember(loaded, "Password", string.Empty);
        RoomHarness.SetMember(
            loaded,
            "Tags",
            System.Collections.Immutable.ImmutableArray<string>.Empty
        );
        var logger = new CapturingLogger<Turbo.Primitives.Rooms.Grains.IRoomGrain>();
        RoomHarness.SetField(_room.Harness.Room, "_logger", logger);
        var changed = await _room.Harness.Room.ModerateRoomBySystemAsync(
            2,
            true,
            "Inappropriate to hotel management",
            true,
            CancellationToken.None
        );

        logger.AtLeast(Microsoft.Extensions.Logging.LogLevel.Warning).Should().BeEmpty();
        changed.Should().BeTrue();
        var snapshot = RoomHarness.GetMember(_room.Harness.State, "RoomSnapshot")!;
        RoomHarness.GetMember(snapshot, "DoorMode").Should().Be(RoomDoorModeType.Locked);
        RoomHarness.GetMember(snapshot, "Name").Should().Be("Inappropriate to hotel management");
        await using (var ctx = db.CreateDbContext())
            ctx.Rooms.Single().DoorMode.Should().Be(RoomDoorModeType.Locked);
        new[] { OWNER, 2, 3 }.Select(_room.IsInRoom).Should().Equal(true, true, false);
    }

    [Fact]
    public async Task RoomKickAll_NeedsItsNode_NotAControllerLevelInTheRoom()
    {
        _room.AddPlayer(OWNER);
        _room.AddPlayer(2);
        _room.AddPlayer(3);

        await _room.SayAsync(2, ":roomkickall");

        _room.IsInRoom(3).Should().BeTrue();
        _room.WhispersTo(2).Should().Equal("You can't use that command.");
    }

    [Fact]
    public async Task RoomKickAll_InARoomOfOnlyTheOwnerAndStaff_SaysNobodyLeft()
    {
        _room.AddPlayer(OWNER);
        _room.AddPlayer(2, PermissionNodes.Command.ROOMKICKALL, PermissionNodes.Room.MODERATE_ANY);

        await _room.SayAsync(2, ":roomkickall");

        _room.WhispersTo(2).Should().Equal("0 visitors were sent out of the room.");
        SessionsClosed.Should().Be(0);
        _room.Harness.Fakes.Log.On<IPlayerNoticeService>().Should().BeEmpty();
    }

    [Fact]
    public async Task RoomMute_SilencesTheRoom_AndRoomUnmuteLetsItSpeakAgain()
    {
        // Staff, who the room mute does not silence: or they could not unmute what they muted.
        _room.AddPlayer(
            2,
            PermissionNodes.Command.ROOMMUTE,
            PermissionNodes.Command.ROOMUNMUTE,
            PermissionNodes.Room.MODERATE_ANY
        );

        await _room.SayAsync(2, ":roommute");
        var mutedAfterMute = IsMuted;
        await _room.SayAsync(2, ":roomunmute");

        mutedAfterMute.Should().BeTrue();
        IsMuted.Should().BeFalse();
        _room.WhispersTo(2).Should().Equal("The room is muted.", "The room is no longer muted.");
    }

    [Fact]
    public async Task RoomMute_SaysSoWhenTheRoomAlreadyIsAsAsked()
    {
        // Staff, who the room mute does not silence: or they could not unmute what they muted.
        _room.AddPlayer(
            2,
            PermissionNodes.Command.ROOMMUTE,
            PermissionNodes.Command.ROOMUNMUTE,
            PermissionNodes.Room.MODERATE_ANY
        );

        await _room.SayAsync(2, ":roomunmute");
        await _room.SayAsync(2, ":roommute");
        await _room.SayAsync(2, ":roommute");

        _room
            .WhispersTo(2)
            .Should()
            .Equal("The room is not muted.", "The room is muted.", "The room is already muted.");
    }

    [Fact]
    public async Task RoomMute_TellsTheRoom_ThroughTheSameMessageTheOwnersToggleSends()
    {
        _room.AddPlayer(2, PermissionNodes.Command.ROOMMUTE);

        await _room.SayAsync(2, ":roommute");

        _room
            .Harness.Fakes.Log.Of("OnNextAsync")
            .SelectMany(c =>
                ((Turbo.Primitives.Rooms.Snapshots.RoomOutboundSnapshot)c.Args[0]!).Composers
            )
            .OfType<Turbo.Primitives.Messages.Outgoing.Roomsettings.MuteAllInRoomEventMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.IsMuted.Should()
            .BeTrue();
    }

    [Fact]
    public async Task UnloadRoom_SendsEveryoneOut_TheOwnerAndTheExecutorToo()
    {
        _room.AddPlayer(OWNER);
        _room.AddPlayer(2, PermissionNodes.Command.UNLOADROOM);
        _room.AddPlayer(3);

        await _room.SayAsync(2, ":unloadroom");

        new[] { OWNER, 2, 3 }.Select(_room.IsInRoom).Should().Equal(false, false, false);
        SessionsClosed.Should().Be(3);
        _room
            .Harness.Fakes.Log.On<IPlayerNoticeService>()
            .Where(x => (PlayerId)x.Args[0]! != (PlayerId)2)
            .Should()
            .BeEmpty();
        _room
            .Harness.Fakes.Log.On<IPlayerNoticeService>()
            .Where(x => (PlayerId)x.Args[0]! == (PlayerId)2)
            .Should()
            .ContainSingle(x => (string)x.Args[1]! == "command.unloadroom.unloaded");
    }

    [Theory]
    [InlineData("rotate", SpecialRoomEffectType.Rotate)]
    [InlineData("SHAKE", SpecialRoomEffectType.Shake)]
    [InlineData("flip", SpecialRoomEffectType.Flip)]
    [InlineData("disco", SpecialRoomEffectType.Disco)]
    public async Task RoomEffect_PlaysTheEffectForEveryoneInTheRoom(
        string name,
        SpecialRoomEffectType effect
    )
    {
        _room.AddPlayer(2, PermissionNodes.Command.ROOMEFFECT);
        _room.AddPlayer(3);

        await _room.SayAsync(2, $":roomeffect {name}");

        EffectsSentToRoom().Should().Equal(effect);
        _room.WhispersTo(2).Should().Equal($"Playing {name.ToLowerInvariant()} for the room.");
        _room.SaidToRoom().Should().BeEmpty("the command is not said aloud");
    }

    [Fact]
    public async Task RoomEffect_IsRefusedWithoutTheNodeOrAnEffectTheClientKnows()
    {
        _room.AddPlayer(2);
        _room.AddPlayer(3, PermissionNodes.Command.ROOMEFFECT);

        await _room.SayAsync(2, ":roomeffect shake");
        await _room.SayAsync(3, ":roomeffect snow");

        EffectsSentToRoom().Should().BeEmpty();
        _room.WhispersTo(2).Should().Equal("You can't use that command.");
        _room
            .WhispersTo(3)
            .Should()
            .Equal("snow is not a valid effect. Usage: :roomeffect <effect>");
    }

    private IEnumerable<SpecialRoomEffectType> EffectsSentToRoom() =>
        _room
            .Harness.Fakes.Log.Of("OnNextAsync")
            .SelectMany(c =>
                ((Turbo.Primitives.Rooms.Snapshots.RoomOutboundSnapshot)c.Args[0]!).Composers
            )
            .OfType<SpecialRoomEffectMessageComposer>()
            .Select(c => c.Effect);
}
