using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;
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
    private const int LEVEL_UP = 32;
    private const int HP = 11;
    private const int FX = 33;
    private const int HP_SHARE = 34;
    private const int OWN_LEVEL_UP = 41;

    private readonly WiredRoom _shop = new(8, 8);
    private readonly WiredRoom _other = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WiredVariableAddonSharingTests()
    {
        _other.Harness.State.UseRoomId(2);

        foreach (var room in new[] { _shop, _other })
        {
            room.Harness.Fakes.Instances[(typeof(IRoomGrain), (object)1L)] = _shop.Harness.Room;
            room.Harness.Fakes.Instances[(typeof(IRoomGrain), (object)2L)] = _other.Harness.Room;
            room.Harness.Fakes.Handlers["GetActiveRoomIdsAsync"] = _ =>
                Task.FromResult(ImmutableArray.Create((RoomId)1, (RoomId)2));
        }

        using var db = _other.Harness.Room._dbCtxFactory.CreateDbContext();

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

    /// <summary>
    /// Nothing is shared before the first Synchronize (official client capture lab61 (c),
    /// WIN63-202610201032: the importing room showed plain "1", no text connector, until then).
    /// </summary>
    [Fact]
    public async Task Nothing_is_shared_before_the_first_synchronize()
    {
        await BuildShopAsync(shared: [1, 1, 0], levelUp: true, synchronize: false);
        var reference = await AddReferenceAsync(imported: [1, 1, 1]);
        await TickAsync(_other);

        reference.GetVarSnapshot().TextConnectors.Should().BeEmpty();
        OtherVariable("linked.current_level").Should().BeNull();
    }

    /// <summary>
    /// What a Synchronize took holds until the next, also for a room that loads it afresh: the
    /// connector edited to "1=Uno" and the Share's text connector tick taken off, without a
    /// Synchronize, a new reference still shows "One" (lab61 (c): "still One" / "untick without
    /// sync -> still Uno").
    /// </summary>
    [Fact]
    public async Task Edits_and_share_ticks_wait_for_the_next_synchronize()
    {
        await BuildShopAsync(shared: [1, 0, 0]);

        (await _shop.SaveAsync<UpdateAddonMessage>(CONNECTOR, stringParam: "1=Uno"))
            .Should()
            .BeTrue();
        (await _shop.SaveAsync<UpdateAddonMessage>(SHARE, intParams: [0, 0, 0])).Should().BeTrue();
        var reference = await AddReferenceAsync(imported: [1, 0, 0]);

        reference
            .GetVarSnapshot()
            .TextConnectors.Should()
            .ContainKey(1)
            .WhoseValue.Should()
            .Be("One");
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

    /// <summary>
    /// Habbo answers the button with WiredSaveSuccess (official client capture lab60,
    /// WIN63-202610201032, 163 ms after it); a refused synchronize is answered with nothing.
    /// </summary>
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task A_synchronize_is_answered_with_save_success(bool synchronized, int answers)
    {
        var harness = new PacketHarness();

        harness.Fakes.Handlers[nameof(IRoomGrain.SynchronizeSharedWiredAddonsAsync)] = _ =>
            Task.FromResult(synchronized);

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("SynchronizeSharedAddonsMessageEvent"),
            PacketHarness.Payload(w => w.Int(SHARE)),
            playerId: 105,
            roomId: 1
        );

        replies
            .Count(x => x.Header == PacketHarness.Outgoing("WiredSaveSuccessMessageComposer"))
            .Should()
            .Be(answers);
    }

    /// <summary>
    /// A fresh share or import box opens with every category ticked (official client capture lab60,
    /// WIN63-202610201032: both open with [1,1,1]).
    /// </summary>
    [Fact]
    public async Task Fresh_share_and_import_addons_tick_every_category()
    {
        var share = _shop.AddBox<WiredAddonVariableShare>(SHARE, 1, 1, "wf_xtra_var_share");
        var import = _shop.AddBox<WiredAddonVariableImport>(IMPORT, 2, 2, "wf_xtra_var_import");

        foreach (var box in new FurnitureWiredAddonLogic[] { share, import })
        {
            await box.LoadWiredAsync(Ct);

            var snapshot = box.GetSnapshot();

            snapshot.IntParams.Should().Equal(1, 1, 1);
            snapshot.DefaultIntParams.Should().Equal(1, 1, 1);
        }

        share
            .Categories.Should()
            .Be(
                WiredVariableAddonCategories.TextConnectors
                    | WiredVariableAddonCategories.SubVariableCreators
                    | WiredVariableAddonCategories.VariableFx
            );
    }

    /// <summary>
    /// A Level Up System on the shared "coins" is a subvariable creator: with both add-ons
    /// ticking the category, the reference has "linked.current_level" and friends, worked out
    /// from its own copy of the value by the shop's table (linear, 100 XP a level: 250 -> 3).
    /// </summary>
    [Fact]
    public async Task An_imported_subvariable_creator_makes_its_subvariables_on_the_reference()
    {
        await BuildShopAsync(shared: [0, 1, 0], levelUp: true);
        await AddReferenceAsync(imported: [0, 1, 0]);
        await TickAsync(_other);

        var level = OtherVariable("linked.current_level");

        level.Should().NotBeNull();
        level!
            .TryGetValue(
                new WiredVariableKey(
                    level.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.Global,
                    0
                ),
                out var value
            )
            .Should()
            .BeTrue();
        ((int)value).Should().Be(3);
        OtherVariable("linked.max_level").Should().NotBeNull();
    }

    /// <summary>
    /// A Level Up System on the reference's own stack goes before the imported one, as an own
    /// text connector does: its table (linear, 50 XP a level: 250 -> 6) is the one that counts,
    /// and the subvariable is there once.
    /// </summary>
    [Fact]
    public async Task An_own_subvariable_creator_goes_before_the_imported_one()
    {
        await BuildShopAsync(shared: [0, 1, 0], levelUp: true);
        _other.AddBox<WiredAddonVariableLevelUp>(OWN_LEVEL_UP, 3, 3, "wf_xtra_var_lvlup_system");
        (await _other.SaveAsync<UpdateAddonMessage>(OWN_LEVEL_UP, intParams: [255, 1, 50, 50]))
            .Should()
            .BeTrue();
        await AddReferenceAsync(imported: [0, 1, 0]);
        await TickAsync(_other);

        var levels = _other
            .Harness.Module<RoomWiredSystem>()
            .GetAllVariables()
            .Where(x => x.GetVarSnapshot().VariableName == "linked.current_level")
            .ToList();

        levels.Should().ContainSingle();
        levels[0]
            .TryGetValue(
                new WiredVariableKey(
                    levels[0].GetVarSnapshot().VariableId,
                    WiredVariableTargetType.Global,
                    0
                ),
                out var value
            )
            .Should()
            .BeTrue();
        ((int)value).Should().Be(6);
    }

    [Theory]
    [InlineData(new[] { 1, 0, 1 }, new[] { 1, 1, 1 })]
    [InlineData(new[] { 1, 1, 1 }, new[] { 1, 0, 1 })]
    public async Task Subvariable_creators_pass_only_when_both_addons_tick_them(
        int[] shared,
        int[] imported
    )
    {
        await BuildShopAsync(shared, levelUp: true);
        await AddReferenceAsync(imported);
        await TickAsync(_other);

        OtherVariable("linked.current_level").Should().BeNull();
    }

    /// <summary>
    /// A Health Points fx on a shared user variable, shared and imported as Variable FX: a player
    /// in the importing room holding the reference is drawn with it, under the shop box's id.
    /// </summary>
    [Fact]
    public async Task An_imported_variable_fx_is_drawn_in_the_importing_room()
    {
        var hp = _shop.AddBox<WiredVariableUser>(HP, 5, 5, "wf_var_user");
        var fx = _shop.AddBox<WiredAddonVariableFxHealthPoints>(FX, 5, 5, "wf_xtra_varfx_hp");
        _shop.AddBox<WiredAddonVariableShare>(HP_SHARE, 5, 5, "wf_xtra_var_share");

        (await _shop.SaveAsync<UpdateAddonMessage>(FX, intParams: [.. fx.GetDefaultIntParams()]))
            .Should()
            .BeTrue();
        (await _shop.SaveAsync<UpdateAddonMessage>(HP_SHARE, intParams: [0, 0, 1]))
            .Should()
            .BeTrue();
        (
            await _shop.SaveAsync<UpdateVariableMessage>(
                HP,
                intParams: [(int)WiredAvailabilityType.Shared, 1],
                stringParam: "hp"
            )
        )
            .Should()
            .BeTrue();
        await hp.LoadWiredAsync(Ct);
        (await SynchronizeAsync(HP_SHARE)).Should().BeTrue();

        var reference = await AddReferenceAsync(imported: [0, 0, 1], source: HP);
        _other.Enter(1, 1, 1);

        (
            await reference.GiveValueAsync(
                new WiredVariableKey(
                    reference.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.User,
                    1
                ),
                new WiredVariableValue(40)
            )
        )
            .Should()
            .BeTrue();
        await TickAsync(_other);

        for (var i = 0; i < 3; i++)
        {
            _now += 2_000;
            await _other.Harness.Room.VariableFxSystem.ProcessAsync(_now, Ct);
        }

        var sent = _other.Harness.Fakes.SentToPlayer<IComposer>(101).ToList();

        sent.OfType<VariableFxConfigsMessageComposer>()
            .SelectMany(x => x.Configs)
            .Should()
            .Contain(x => x.ConfigId == FX);
        sent.OfType<VariableFxStatusMessageComposer>()
            .SelectMany(x => x.Statuses)
            .Should()
            .Contain(x => x.Key.ConfigId == FX && x.Value == 40);
    }

    private IWiredVariable? OtherVariable(string name) =>
        _other
            .Harness.Module<RoomWiredSystem>()
            .GetAllVariables()
            .FirstOrDefault(x => x.GetVarSnapshot().VariableName == name);

    private async Task BuildShopAsync(int[] shared, bool levelUp = false, bool synchronize = true)
    {
        var coins = _shop.AddBox<WiredVariableRoom>(COINS, 6, 6, "wf_var_room");

        if (levelUp)
        {
            _shop.AddBox<WiredAddonVariableLevelUp>(LEVEL_UP, 6, 6, "wf_xtra_var_lvlup_system");

            // Every subvariable, linear, 100 XP a level, 50 levels.
            (await _shop.SaveAsync<UpdateAddonMessage>(LEVEL_UP, intParams: [255, 1, 100, 50]))
                .Should()
                .BeTrue();
        }

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

        if (levelUp)
            (
                await coins.SetValueAsync(
                    _shop.Harness.Fakes.Create<IWiredExecutionContext>(),
                    new WiredVariableKey(
                        coins.GetVarSnapshot().VariableId,
                        WiredVariableTargetType.Global,
                        0
                    ),
                    new WiredVariableValue(250)
                )
            )
                .Should()
                .BeTrue();

        if (synchronize)
            (await SynchronizeAsync(SHARE)).Should().BeTrue();
    }

    private Task<bool> SynchronizeAsync(int shareId) =>
        _shop.Harness.Room.SynchronizeSharedWiredAddonsAsync(
            ActionContext.CreateForSystem(1),
            shareId,
            Ct
        );

    private async Task<WiredVariableReference> AddReferenceAsync(
        int[]? imported,
        int source = COINS
    )
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
                    ((FurnitureWiredVariableLogic)_shop.FloorItem(source).Logic)
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
