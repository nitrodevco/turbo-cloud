using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Wired.Link;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A player's WIRED Links networks, as the official hotel answers the Link window (official client
/// capture 2026-10-21, build WIN63-202610201032, own rooms): what each request changes, refuses
/// and lists, and where a send goes.
/// </summary>
public sealed class WiredLinkOwnerGrainTests : IDisposable
{
    private const int OWNER = 7;
    private const int ROOM_A = 80462556;
    private const int ROOM_B = 80463631;
    private const int LINK_A = 2147418112;
    private const int LINK_A2 = 2147418113;
    private const int LINK_B = 2147418200;

    private static readonly WiredLinkEndpoint A = new(ROOM_A, LINK_A);
    private static readonly WiredLinkEndpoint A2 = new(ROOM_A, LINK_A2);
    private static readonly WiredLinkEndpoint B = new(ROOM_B, LINK_B);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WiredLinkOwnerGrainTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = OWNER,
                Name = "link-owner",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "link-model",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        _db.Insert(NewRoom(ROOM_A, "Hutch.'s  room"));
        _db.Insert(NewRoom(ROOM_B, "duo test"));
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_created_network_is_answered_alone_and_the_window_lists_networks_by_name()
    {
        var grain = await NewGrainAsync();

        var outcome = await grain.CreateNetworkAsync(A, "LabMax 10", Ct);
        await grain.CreateNetworkAsync(A, "LabMax 1", Ct);
        await grain.CreateNetworkAsync(A, "LabMax 100", Ct);

        outcome.ResultCode.Should().Be(WiredLinkResultCodes.OK);
        outcome.SelectedNetworkId.Should().Be("0");
        outcome.Networks.Should().ContainSingle();
        outcome.Networks[0].Name.Should().Be("LabMax 10");
        outcome.Networks[0].ConnectionCount.Should().Be(0);
        outcome.Networks[0].LinkedInCurrentRoom.Should().BeFalse();

        var configuration = await grain.GetConfigurationAsync(A, Ct);

        configuration
            .Networks.Select(x => x.Name)
            .Should()
            .Equal("LabMax 1", "LabMax 10", "LabMax 100");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_name_without_text_is_refused_with_code_3(string name)
    {
        var grain = await NewGrainAsync();

        (await grain.CreateNetworkAsync(A, name, Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.INVALID_NAME);
    }

    [Fact]
    public async Task A_name_may_be_255_characters_and_not_256()
    {
        var grain = await NewGrainAsync();

        (await grain.CreateNetworkAsync(A, new string('n', 255), Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.OK);
        (await grain.CreateNetworkAsync(A, new string('m', 256), Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.INVALID_NAME);
    }

    [Fact]
    public async Task Only_an_exact_duplicate_name_is_refused_and_names_keep_their_spaces()
    {
        var grain = await NewGrainAsync();

        await grain.CreateNetworkAsync(A, "labnet a", Ct);

        (await grain.CreateNetworkAsync(A, "labnet a", Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.DUPLICATE_NAME);
        (await grain.CreateNetworkAsync(A, "LabNet A", Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.OK);

        var spaced = await grain.CreateNetworkAsync(A, " LabNet A ", Ct);

        spaced.ResultCode.Should().Be(WiredLinkResultCodes.OK);
        spaced.Networks[0].Name.Should().Be(" LabNet A ");
    }

    [Fact]
    public async Task Past_the_configured_limit_a_create_is_refused_with_code_8()
    {
        var grain = await NewGrainAsync(new WiredConfig { LinkNetworkLimit = 2 });

        await grain.CreateNetworkAsync(A, "one", Ct);
        await grain.CreateNetworkAsync(A, "two", Ct);

        (await grain.CreateNetworkAsync(A, "three", Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.NETWORK_LIMIT);
    }

    [Fact]
    public async Task A_save_answers_the_link_configuration_and_the_networks_it_changed()
    {
        var grain = await NewGrainAsync();
        var first = await CreateAsync(grain, "first");
        var second = await CreateAsync(grain, "second");

        var joined = await grain.SaveAsync(A, true, first, true, Ct);

        joined.ResultCode.Should().Be(WiredLinkResultCodes.OK);
        joined.SelectedNetworkId.Should().Be(first);
        joined.ReceiveLocal.Should().BeTrue();
        joined.Networks.Should().ContainSingle();
        joined.Networks[0].ConnectionCount.Should().Be(1);
        joined.Networks[0].LinkedInCurrentRoom.Should().BeTrue();

        var switched = await grain.SaveAsync(A, true, second, true, Ct);

        switched.Networks.Should().HaveCount(2);
        switched
            .Networks.Single(x => x.NetworkId == first)
            .Should()
            .Match<WiredLinkNetworkSnapshot>(x => x.ConnectionCount == 0 && !x.LinkedInCurrentRoom);
        switched
            .Networks.Single(x => x.NetworkId == second)
            .Should()
            .Match<WiredLinkNetworkSnapshot>(x => x.ConnectionCount == 1 && x.LinkedInCurrentRoom);
    }

    [Fact]
    public async Task Receiving_its_own_room_without_a_network_saves_with_no_network_changed()
    {
        var grain = await NewGrainAsync();

        var outcome = await grain.SaveAsync(A, false, "0", true, Ct);

        outcome.ResultCode.Should().Be(WiredLinkResultCodes.OK);
        outcome.SelectedNetworkId.Should().Be("0");
        outcome.ReceiveLocal.Should().BeTrue();
        outcome.Networks.Should().BeEmpty();
    }

    [Fact]
    public async Task A_second_link_of_a_room_on_the_same_network_is_refused_with_code_5()
    {
        var grain = await NewGrainAsync();
        var network = await CreateAsync(grain, "shared");

        await grain.SaveAsync(A, false, network, false, Ct);

        (await grain.SaveAsync(A2, false, network, false, Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.NETWORK_IN_USE_IN_ROOM);
        (await grain.SaveAsync(B, false, network, false, Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.OK);
    }

    [Fact]
    public async Task An_unknown_network_is_code_2_and_a_malformed_id_code_6()
    {
        var grain = await NewGrainAsync();

        (await grain.SaveAsync(A, false, "999999", false, Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.NETWORK_NOT_FOUND);
        (await grain.SaveAsync(A, false, "abc", false, Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.INVALID_NETWORK_ID);
        (await grain.RenameNetworkAsync(A, "999999", "x", Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.NETWORK_NOT_FOUND);
        (await grain.DeleteNetworkAsync(A, "abc", Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.INVALID_NETWORK_ID);
        (await grain.GetConnectionsAsync("999999", Ct)).Should().BeNull();
    }

    [Fact]
    public async Task A_rename_to_its_own_name_passes_and_to_another_networks_name_is_code_4()
    {
        var grain = await NewGrainAsync();
        var mine = await CreateAsync(grain, "mine");
        await CreateAsync(grain, "other");

        (await grain.RenameNetworkAsync(A, mine, "mine", Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.OK);
        (await grain.RenameNetworkAsync(A, mine, "other", Ct))
            .ResultCode.Should()
            .Be(WiredLinkResultCodes.DUPLICATE_NAME);

        var renamed = await grain.RenameNetworkAsync(A, mine, "renamed", Ct);

        renamed.Networks.Should().ContainSingle().Which.Name.Should().Be("renamed");
    }

    [Fact]
    public async Task A_delete_answers_the_asking_link_and_names_the_other_links_it_disconnected()
    {
        var grain = await NewGrainAsync();
        var gone = await CreateAsync(grain, "gone");
        var kept = await CreateAsync(grain, "kept");

        await grain.SaveAsync(A, false, kept, true, Ct);
        await grain.SaveAsync(A2, false, gone, true, Ct);
        await grain.SaveAsync(B, false, gone, false, Ct);

        var outcome = await grain.DeleteNetworkAsync(A, gone, Ct);

        outcome.ResultCode.Should().Be(WiredLinkResultCodes.OK);
        outcome.RemovedNetworkId.Should().Be(gone);
        outcome.SelectedNetworkId.Should().Be(kept);
        outcome.Disconnected.Should().BeEquivalentTo([A2, B]);

        // The other Link reopens disconnected and keeps its receive setting.
        var reopened = await grain.GetConfigurationAsync(A2, Ct);

        reopened.SelectedNetworkId.Should().Be("0");
        reopened.ReceiveLocal.Should().BeTrue();
        reopened.Networks.Select(x => x.NetworkId).Should().Equal(kept);
    }

    [Fact]
    public async Task Connections_list_each_link_with_its_room_name_and_kind()
    {
        var grain = await NewGrainAsync();
        var network = await CreateAsync(grain, "8");

        await grain.SaveAsync(A, true, network, true, Ct);
        await grain.SaveAsync(B, true, network, false, Ct);

        var connections = (await grain.GetConnectionsAsync(network, Ct))!.Value;

        connections
            .Should()
            .BeEquivalentTo([
                new WiredLinkConnectionSnapshot
                {
                    RoomId = ROOM_A,
                    RoomName = "Hutch.'s  room",
                    FurniId = LINK_A,
                    IsBuildersClub = true,
                    ReceiveLocal = true,
                },
                new WiredLinkConnectionSnapshot
                {
                    RoomId = ROOM_B,
                    RoomName = "duo test",
                    FurniId = LINK_B,
                    IsBuildersClub = true,
                    ReceiveLocal = false,
                },
            ]);
    }

    [Fact]
    public async Task Networks_and_connections_outlive_the_grain()
    {
        var grain = await NewGrainAsync();
        var network = await CreateAsync(grain, "kept");

        await grain.SaveAsync(A, false, network, true, Ct);

        var again = await NewGrainAsync();
        var configuration = await again.GetConfigurationAsync(A, Ct);

        configuration.SelectedNetworkId.Should().Be(network);
        configuration.ReceiveLocal.Should().BeTrue();
        configuration.Networks.Should().ContainSingle().Which.ConnectionCount.Should().Be(1);
    }

    [Fact]
    public async Task A_send_reaches_the_other_rooms_on_the_network_and_itself_only_when_it_receives_its_room()
    {
        var grain = await NewGrainAsync();
        var network = await CreateAsync(grain, "net");

        await grain.SaveAsync(A, false, network, false, Ct);
        await grain.SaveAsync(B, false, network, false, Ct);
        LoadedRooms(ROOM_A, ROOM_B);

        (await grain.SendAsync(ROOM_A, [LINK_A], Message("hi"), Ct)).Should().Be(1);
        Delivered().Should().Equal((ROOM_B, LINK_B));

        await grain.SaveAsync(A, false, network, true, Ct);

        (await grain.SendAsync(ROOM_A, [LINK_A], Message("hi"), Ct)).Should().Be(2);
    }

    [Fact]
    public async Task A_send_to_a_room_that_is_not_loaded_is_dropped()
    {
        var grain = await NewGrainAsync();
        var network = await CreateAsync(grain, "net");

        await grain.SaveAsync(A, false, network, false, Ct);
        await grain.SaveAsync(B, false, network, false, Ct);
        LoadedRooms(ROOM_A);

        (await grain.SendAsync(ROOM_A, [LINK_A], Message("hi"), Ct)).Should().Be(0);
        Delivered().Should().BeEmpty();
    }

    [Fact]
    public async Task A_link_with_no_saved_configuration_sends_nowhere()
    {
        var grain = await NewGrainAsync();

        LoadedRooms(ROOM_A);

        (await grain.SendAsync(ROOM_A, [LINK_A], Message("hi"), Ct)).Should().Be(0);
    }

    private static WiredLinkPayload Message(string text) =>
        new() { SourceRoomId = ROOM_A, Message = text };

    private void LoadedRooms(params int[] roomIds) =>
        _fakes.Handlers[nameof(IRoomDirectoryGrain.GetActiveRoomIdsAsync)] = _ =>
            Task.FromResult(roomIds.Select(x => (RoomId)x).ToImmutableArray());

    private List<(int Room, int Link)> Delivered() =>
        [
            .. _fakes
                .Log.Of(nameof(IRoomGrain.ReceiveWiredLinkPayloadAsync))
                .SelectMany(call =>
                    ((ImmutableArray<int>)call.Args[0]!).Select(link =>
                        (Convert.ToInt32(call.Key), link)
                    )
                ),
        ];

    private static async Task<string> CreateAsync(IWiredLinkOwnerGrain grain, string name) =>
        (await grain.CreateNetworkAsync(A, name, Ct)).Networks.Single().NetworkId;

    private async Task<IWiredLinkOwnerGrain> NewGrainAsync(WiredConfig? config = null)
    {
        var grain = GrainHarness.Create(
            typeof(RoomGrain).Assembly,
            "Turbo.Rooms.Wired.Link.WiredLinkOwnerGrain",
            _fakes,
            _db,
            playerId: OWNER
        );

        if (config is not null)
            RoomHarness.SetMember(grain, "_config", config);

        await ((Grain)grain).OnActivateAsync(Ct);

        return (IWiredLinkOwnerGrain)grain;
    }

    private static RoomEntity NewRoom(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
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
        };
}
