using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Variable Add-on: Share" and "Variable Add-on: Import" (Wired Faculty 19/10/2026): "Place a
/// Share Add-on on top of a permanently shared Variable, and place the Import Add-on on top of a
/// WIRED Variable: From Another Room. You will never need to duplicate variable add-ons again!"
/// Room 1 ("Shop") has the shared global "coins" with a Text Connector ("1=One", "_=Many") and a
/// Share add-on on it; room 2, of the same owner, refers to it.
/// </summary>
public sealed class WiredVariableAddonSharingTests
{
    private const int COINS = 10;
    private const int CONNECTOR = 30;
    private const int SHARE = 31;
    private const int REFERENCE = 20;
    private const int IMPORT = 40;

    private readonly WiredRoom _shop = new(8, 8);
    private readonly WiredRoom _other = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WiredVariableAddonSharingTests()
    {
        RoomHarness.SetMember(_other.Harness.State, "RoomId", (RoomId)2);

        foreach (var room in new[] { _shop, _other })
        {
            room.Harness.Fakes.Instances[(typeof(IRoomGrain), (object)1L)] = _shop.Harness.Room;
            room.Harness.Fakes.Instances[(typeof(IRoomGrain), (object)2L)] = _other.Harness.Room;
            room.Harness.Fakes.Handlers["GetActiveRoomIdsAsync"] = _ =>
                Task.FromResult(ImmutableArray.Create((RoomId)1, (RoomId)2));
        }

        using var db = (
            (Microsoft.EntityFrameworkCore.IDbContextFactory<Turbo.Database.Context.TurboDbContext>)
                RoomHarness.GetField(_other.Harness.Room, "_dbCtxFactory")!
        ).CreateDbContext();

        db.Rooms.Add(
            new RoomEntity
            {
                Id = 1,
                Name = "Shop",
                PlayerEntityId = 1,
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
        db.SaveChanges();
    }

    [Fact]
    public async Task A_reference_with_an_import_addon_shows_the_shared_text_connector()
    {
        await BuildShopAsync(shared: [1, 0, 0]);
        var reference = await AddReferenceAsync(imported: [1, 0, 0]);

        var snapshot = reference.GetVarSnapshot();

        snapshot.TextConnectors.Should().ContainKey(1).WhoseValue.Should().Be("One");
        snapshot.TextConnectorFallback.Should().Be("Many");
        snapshot.Flags.Has(WiredVariableFlags.HasTextConnector).Should().BeTrue();
    }

    [Fact]
    public async Task A_reference_without_an_import_addon_has_no_text_connector()
    {
        await BuildShopAsync(shared: [1, 0, 0]);
        var reference = await AddReferenceAsync(imported: null);

        var snapshot = reference.GetVarSnapshot();

        snapshot.TextConnectors.Should().BeEmpty();
        snapshot.TextConnectorFallback.Should().BeNull();
        snapshot.Flags.Has(WiredVariableFlags.HasTextConnector).Should().BeFalse();
    }

    [Theory]
    [InlineData(new[] { 0, 1, 1 }, new[] { 1, 1, 1 })]
    [InlineData(new[] { 1, 1, 1 }, new[] { 0, 1, 1 })]
    public async Task Text_connectors_pass_only_when_both_addons_tick_them(
        int[] shared,
        int[] imported
    )
    {
        await BuildShopAsync(shared);
        var reference = await AddReferenceAsync(imported);

        reference.GetVarSnapshot().TextConnectors.Should().BeEmpty();
    }

    [Fact]
    public async Task Synchronize_passes_a_changed_text_connector_on_to_the_rooms_using_it()
    {
        await BuildShopAsync(shared: [1, 0, 0]);
        var reference = await AddReferenceAsync(imported: [1, 0, 0]);

        (await _shop.SaveAsync<UpdateAddonMessage>(CONNECTOR, stringParam: "1=Uno"))
            .Should()
            .BeTrue();

        (
            await _shop.Harness.Room.SynchronizeSharedWiredAddonsAsync(
                ActionContext.CreateForSystem(1),
                SHARE,
                Ct
            )
        )
            .Should()
            .BeTrue();
        await TickAsync(_other);

        reference
            .GetVarSnapshot()
            .TextConnectors.Should()
            .ContainKey(1)
            .WhoseValue.Should()
            .Be("Uno");
    }

    [Fact]
    public async Task A_visitor_cannot_synchronize()
    {
        await BuildShopAsync(shared: [1, 0, 0]);

        (
            await _shop.Harness.Room.SynchronizeSharedWiredAddonsAsync(
                ActionContext.CreateForPlayer((PlayerId)999, (RoomId)1),
                SHARE,
                Ct
            )
        )
            .Should()
            .BeFalse();
    }

    /// <summary>
    /// The button's packet (Flash <c>SynchronizeSharedAddonsMessageComposer(wiredId)</c>, one int)
    /// reaches the room the player is in, for the box being edited.
    /// </summary>
    [Fact]
    public async Task The_synchronize_packet_asks_the_players_room_for_the_box()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("SynchronizeSharedAddonsMessageEvent"),
            PacketHarness.Payload(w => w.Int(SHARE)),
            playerId: 105,
            roomId: 1
        );

        var call = harness
            .Fakes.Log.Of(nameof(IRoomGrain.SynchronizeSharedWiredAddonsAsync))
            .Should()
            .ContainSingle()
            .Subject;

        call.Key.Should().Be(1L);
        call.Args[1].Should().Be(SHARE);
    }

    private async Task BuildShopAsync(int[] shared)
    {
        var coins = _shop.AddBox<WiredVariableRoom>(COINS, 6, 6, "wf_var_room");
        _shop.AddBox<WiredAddonVariableTextConnector>(
            CONNECTOR,
            6,
            6,
            "wf_xtra_var_text_connector"
        );
        _shop.AddBox<WiredAddonVariableShare>(SHARE, 6, 6, "wf_xtra_var_share");

        (await _shop.SaveAsync<UpdateAddonMessage>(CONNECTOR, stringParam: "1=One\n_=Many"))
            .Should()
            .BeTrue();
        (await _shop.SaveAsync<UpdateAddonMessage>(SHARE, intParams: shared)).Should().BeTrue();
        (
            await _shop.SaveAsync<UpdateVariableMessage>(
                COINS,
                intParams: [(int)WiredAvailabilityType.Shared],
                stringParam: "coins"
            )
        )
            .Should()
            .BeTrue();
        await coins.LoadWiredAsync(Ct);
    }

    private async Task<WiredVariableReference> AddReferenceAsync(int[]? imported)
    {
        var reference = _other.AddBox<WiredVariableReference>(REFERENCE, 3, 3, "wf_var_reference");

        if (imported is not null)
        {
            _other.AddBox<WiredAddonVariableImport>(IMPORT, 3, 3, "wf_xtra_var_import");

            (await _other.SaveAsync<UpdateAddonMessage>(IMPORT, intParams: imported))
                .Should()
                .BeTrue();
        }

        (
            await _other.SaveAsync<UpdateVariableMessage>(
                REFERENCE,
                intParams: [0],
                stringParam: "linked",
                variableIds:
                [
                    ((FurnitureWiredVariableLogic)_shop.FloorItem(COINS).Logic)
                        .GetVarSnapshot()
                        .VariableId.ToString(),
                ]
            )
        )
            .Should()
            .BeTrue();
        await reference.LoadWiredAsync(Ct);

        return reference;
    }

    private async Task TickAsync(WiredRoom room)
    {
        _now += 1_000;
        await room.Harness.Module<RoomWiredSystem>().ProcessWiredAsync(_now, dormant: false, Ct);
    }
}
