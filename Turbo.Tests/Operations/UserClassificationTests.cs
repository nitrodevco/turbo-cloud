using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Players;
using Turbo.Operations;
using Turbo.Operations.Configuration;
using Turbo.PacketHandlers.Userclassification;
using Turbo.Primitives.Messages.Incoming.Userclassification;
using Turbo.Primitives.Messages.Outgoing.Userclassification;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Operations;

/// <summary>
/// <c>:uc</c> and <c>:anew</c> (1318 AS3 <c>ChatInputWidgetHandler</c>): the client sends the class
/// it wants as one string (<c>RoomUsersClassificationMessageComposer</c>,
/// <c>PeerUsersClassificationMessageComposer</c>) and reads a count, then each player's id, name
/// and class (<c>UserClassificationMessageEvent</c>), opening the classification window with them.
/// </summary>
public sealed class UserClassificationTests : IDisposable
{
    private const int STAFF = 1;
    private const int NEWBIE = 2;
    private const int MEMBER = 3;
    private const int PLAIN = 4;
    private const int ELSEWHERE = 5;
    private const int ROOM = 9;

    private static readonly DateTime NOW = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly HashSet<string> _held = [PermissionNodes.Room.MODERATE_ANY];
    private readonly HashSet<int> _club = [STAFF, MEMBER];
    private readonly List<int> _inRoom = [STAFF, NEWBIE, MEMBER, PLAIN];
    private readonly List<int> _online = [STAFF, NEWBIE, MEMBER, PLAIN, ELSEWHERE];
    private OperationsConfig _config = new();

    public UserClassificationTests()
    {
        _db.Insert(Player(STAFF, "staff", NOW.AddDays(-30)));
        _db.Insert(Player(NEWBIE, "newbie", NOW.AddDays(-2)));
        _db.Insert(Player(MEMBER, "member", NOW.AddDays(-100)));
        _db.Insert(Player(PLAIN, "plain", NOW.AddDays(-7)));
        _db.Insert(Player(ELSEWHERE, "away", NOW.AddHours(-1)));

        _fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(
                Convert.ToInt32(call.Key) == STAFF && _held.Contains((string)call.Args[0]!)
            );
        _fakes.Handlers["HasActiveAsync"] = call =>
            Task.FromResult(_club.Contains(Convert.ToInt32(call.Key)));
        _fakes.Handlers["GetRoomPlayersAsync"] = _ =>
            Task.FromResult(ImmutableArray.CreateRange(_inRoom.Select(x => (PlayerId)x)));
        _fakes.Handlers["GetOnlinePlayerIds"] = _ =>
            (IReadOnlyCollection<PlayerId>)[.. _online.Select(x => (PlayerId)x)];
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("RoomUsersClassificationMessageEvent")]
    [InlineData("PeerUsersClassificationMessageEvent")]
    public void Both_requests_carry_the_class_as_one_string(string header)
    {
        var message = PacketHarness.Parse(
            PacketHarness.Incoming(header),
            PacketHarness.Payload(w => w.String("new"))
        );

        var classification = message switch
        {
            RoomUsersClassificationMessage room => room.Classification,
            PeerUsersClassificationMessage peer => peer.Classification,
            _ => null,
        };
        classification.Should().Be("new");
    }

    [Fact]
    public void The_list_is_a_count_then_id_name_and_class()
    {
        var reply = PacketHarness.Encode(
            new UserClassificationMessageComposer
            {
                Users =
                [
                    new UserClassificationSnapshot
                    {
                        UserId = 7,
                        UserName = "seven",
                        ClassType = UserClassifications.PAYING,
                    },
                    new UserClassificationSnapshot
                    {
                        UserId = 8,
                        UserName = "eight",
                        ClassType = UserClassifications.NEW,
                    },
                ],
            }
        );

        reply.Header.Should().Be(PacketHarness.Outgoing("UserClassificationMessageComposer"));
        reply.PopInt().Should().Be(2);
        reply.PopInt().Should().Be(7);
        reply.PopString().Should().Be("seven");
        reply.PopString().Should().Be("paying");
        reply.PopInt().Should().Be(8);
        reply.PopString().Should().Be("eight");
        reply.PopString().Should().Be("new");
        reply.End.Should().BeTrue();
    }

    [Fact]
    public async Task A_bare_uc_lists_everyone_in_the_room_by_name_with_their_class()
    {
        var users = Read(Assert.Single(await SendRoomAsync(string.Empty)));

        // New wins over club (staff is a member but past seven days; plain is exactly seven).
        users
            .Should()
            .Equal(
                (MEMBER, "member", "paying"),
                (NEWBIE, "newbie", "new"),
                (PLAIN, "plain", "regular"),
                (STAFF, "staff", "paying")
            );
    }

    [Fact]
    public async Task A_new_user_with_club_is_still_new()
    {
        _club.Add(NEWBIE);

        Read(Assert.Single(await SendRoomAsync("new"))).Should().Equal((NEWBIE, "newbie", "new"));
    }

    [Fact]
    public async Task A_class_is_matched_in_any_case()
    {
        Read(Assert.Single(await SendRoomAsync("PAYING")))
            .Should()
            .Equal((MEMBER, "member", "paying"), (STAFF, "staff", "paying"));
    }

    [Fact]
    public async Task A_class_that_is_none_of_them_opens_an_empty_window()
    {
        Read(Assert.Single(await SendRoomAsync("vip"))).Should().BeEmpty();
        _fakes.Log.Of("GetRoomPlayersAsync").Should().ContainSingle();
    }

    [Fact]
    public async Task Asking_for_new_users_never_asks_about_club()
    {
        await SendRoomAsync("new");

        _fakes.Log.Of("HasActiveAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task Club_is_asked_only_of_players_past_the_new_user_window()
    {
        await SendRoomAsync(string.Empty);

        _fakes
            .Log.Of("HasActiveAsync")
            .Select(x => Convert.ToInt32(x.Key))
            .Should()
            .BeEquivalentTo([STAFF, MEMBER, PLAIN]);
    }

    [Fact]
    public async Task An_ambassador_gets_the_new_users_anew_asks_for()
    {
        _held.Clear();

        Read(Assert.Single(await SendRoomAsync("new"))).Should().Equal((NEWBIE, "newbie", "new"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("paying")]
    public async Task An_ambassador_asking_for_anything_else_is_not_answered(string classification)
    {
        _held.Clear();

        (await SendRoomAsync(classification)).Should().BeEmpty();
        _fakes.Log.Of("GetRoomPlayersAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task Nobody_outside_a_room_is_answered()
    {
        (await SendRoomAsync(string.Empty, roomId: -1)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_window_for_new_users_follows_the_configured_days()
    {
        _config = new OperationsConfig { UserClassificationNewUserDays = 31 };

        Read(Assert.Single(await SendRoomAsync("new")))
            .Select(x => x.Id)
            .Should()
            .Equal(NEWBIE, PLAIN, STAFF);
    }

    [Fact]
    public async Task Uc_hotel_lists_the_players_online()
    {
        var users = Read(Assert.Single(await SendHotelAsync("new")));

        users.Should().Equal((ELSEWHERE, "away", "new"), (NEWBIE, "newbie", "new"));
    }

    [Fact]
    public async Task Uc_hotel_names_no_more_than_the_configured_cap()
    {
        _config = new OperationsConfig { UserClassificationMaxUsers = 2 };

        var users = Read(Assert.Single(await SendHotelAsync(string.Empty)));

        users.Select(x => x.Id).Should().BeEquivalentTo([STAFF, NEWBIE]);
    }

    [Fact]
    public async Task A_cap_counts_matches_not_players_looked_at()
    {
        _config = new OperationsConfig { UserClassificationMaxUsers = 2 };

        var users = Read(Assert.Single(await SendHotelAsync("regular")));

        users.Should().Equal((PLAIN, "plain", "regular"));
    }

    [Fact]
    public void Each_request_is_gated_on_what_the_client_sends_it_for()
    {
        Nodes<RoomUsersClassificationMessageHandler>()
            .Should()
            .Equal(PermissionNodes.Role.AMBASSADOR, PermissionNodes.Room.MODERATE_ANY);
        Nodes<PeerUsersClassificationMessageHandler>()
            .Should()
            .Equal(PermissionNodes.Room.MODERATE_ANY);
    }

    [Theory]
    [InlineData(-1, false, "new")]
    [InlineData(-1, true, "new")]
    [InlineData(-7, true, "paying")]
    [InlineData(-7, false, "regular")]
    public void A_player_has_one_class(int registeredDaysAgo, bool hasClub, string expected) =>
        UserClassifications
            .Classify(NOW.AddDays(registeredDaysAgo), hasClub, NOW, TimeSpan.FromDays(7))
            .Should()
            .Be(expected);

    private static IReadOnlyList<string> Nodes<THandler>() =>
        typeof(THandler).GetCustomAttribute<RequiresPermissionAttribute>()!.Nodes;

    private static List<(int Id, string Name, string Class)> Read(ClientPacket reply)
    {
        reply.Header.Should().Be(PacketHarness.Outgoing("UserClassificationMessageComposer"));

        var users = Enumerable
            .Range(0, reply.PopInt())
            .Select(_ => (reply.PopInt(), reply.PopString(), reply.PopString()))
            .ToList();

        reply.End.Should().BeTrue();

        return users;
    }

    private Task<List<ClientPacket>> SendRoomAsync(string classification, int roomId = ROOM) =>
        Harness()
            .SendAsync(
                PacketHarness.Incoming("RoomUsersClassificationMessageEvent"),
                PacketHarness.Payload(w => w.String(classification)),
                playerId: STAFF,
                roomId: roomId
            );

    private Task<List<ClientPacket>> SendHotelAsync(string classification) =>
        Harness()
            .SendAsync(
                PacketHarness.Incoming("PeerUsersClassificationMessageEvent"),
                PacketHarness.Payload(w => w.String(classification)),
                playerId: STAFF
            );

    private PacketHarness Harness()
    {
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(IUserClassificationService)] =
            new UserClassificationService(
                _db,
                _fakes.Create<Orleans.IGrainFactory>(),
                _fakes.Create<ISessionGateway>(),
                new ManualTimeProvider(new DateTimeOffset(NOW)),
                Options.Create(_config),
                NullLogger<UserClassificationService>.Instance
            );
        return harness;
    }

    private static PlayerEntity Player(int id, string name, DateTime createdAt) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
            CreatedAt = createdAt,
        };
}
