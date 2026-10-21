using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.Rooms.Wired.WebApi;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Permanent, shared" variables and "WIRED Variable: From Another Room" (Wired Faculty,
/// variables-info #5). Room 1 ("Shop") has the shared global "coins" and the shared user variable
/// "hp"; room 2, of the same owner, refers to them. Player 105 is in both rooms.
/// </summary>
public sealed class WiredSharedVariableTests
{
    private const int COINS = 10;
    private const int HP = 11;
    private const int REFERENCE = 20;
    private const int SECOND_REFERENCE = 21;
    private const int LOCAL = 12;
    private const int PLAYER_INDEX = 5;

    private readonly WiredRoom _shop = new(8, 8);
    private readonly WiredRoom _other = new(8, 8);
    private WiredVariableRoom _coins = null!;
    private WiredVariableUser _hp = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WiredSharedVariableTests()
    {
        RoomHarness.SetMember(_other.Harness.State, "RoomId", (RoomId)2);

        foreach (var room in new[] { _shop, _other })
        {
            room.Harness.Fakes.Instances[(typeof(IRoomGrain), (object)1L)] = _shop.Harness.Room;
            room.Harness.Fakes.Instances[(typeof(IRoomGrain), (object)2L)] = _other.Harness.Room;
            room.Harness.Fakes.Handlers["GetActiveRoomIdsAsync"] = _ =>
                Task.FromResult(ImmutableArray.Create((RoomId)1, (RoomId)2));
            room.Enter(PLAYER_INDEX, 1, 1);
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
    public async Task The_editor_lists_the_shared_variables_of_the_owners_other_rooms()
    {
        await BuildShopAsync();
        _other.AddBox<WiredVariableReference>(REFERENCE, 3, 3, "wf_var_reference");

        var snapshot = await _other.Harness.Room.WiredSystem.GetBoxSnapshotAsync(REFERENCE, Ct);

        var list = snapshot!.ContextSnapshots.OfType<WiredVariableSharedListSnapshot>().Single();

        list.Variables.Select(x => (x.RoomId, x.RoomName, x.Variable.VariableName))
            .Should()
            .BeEquivalentTo([(1, "Shop", "coins"), (1, "Shop", "hp")]);
    }

    [Fact]
    public async Task A_reference_reads_and_writes_the_global_where_it_lives()
    {
        await BuildShopAsync();
        await SetCoinsAsync(5);
        var reference = await AddReferenceAsync(
            _coins.GetVarSnapshot().VariableId,
            readOnly: false
        );
        var key = Key(reference, WiredVariableTargetType.Global, 0);

        Read(reference, key).Should().Be(5);

        (await reference.SetValueAsync(null!, key, new WiredVariableValue(12))).Should().BeTrue();

        Read(_coins, Key(_coins, WiredVariableTargetType.Global, 0)).Should().Be(12);
    }

    [Fact]
    public async Task The_web_api_reads_a_reference_but_never_writes_the_room_it_lives_in()
    {
        const int ADDON = 30;
        await BuildShopAsync();
        await SetCoinsAsync(5);
        await AddReferenceAsync(_coins.GetVarSnapshot().VariableId, readOnly: false);
        // The room lists the box's variable once its stack is read, as on a move or save.
        await _other
            .Harness.Module<RoomWiredSystem>()
            .OnRoomEventAsync(
                new RoomWiredStackChangedEvent
                {
                    RoomId = 2,
                    CausedBy = ActionContext.CreateForSystem((RoomId)2),
                    StackIds = [_other.Map.ToIdx(3, 3)],
                },
                Ct
            );
        await _other
            .Harness.Module<RoomWiredSystem>()
            .ProcessWiredAsync(10_000, dormant: false, Ct);
        _other.AddBox<WiredAddonVariablesWebApi>(ADDON, 4, 4, "wf_xtra_var_web_api");
        var system = ActionContext.CreateForSystem((RoomId)2);
        var read = await _other.Harness.Room.GenerateWiredWebApiKeyAsync(system, ADDON, true, Ct);
        var write = await _other.Harness.Room.GenerateWiredWebApiKeyAsync(system, ADDON, false, Ct);
        (
            await _other.SaveAsync<UpdateAddonMessage>(
                ADDON,
                intParams: [1],
                stringParam: read + "	" + write
            )
        )
            .Should()
            .BeTrue();

        (
            await _other.Harness.Room.HandleWiredWebApiAsync(
                new WiredWebApiRequest
                {
                    Operation = WiredWebApiOperation.GetGlobal,
                    VariableName = "linked",
                    ReadKey = read,
                },
                Ct
            )
        ).Status.Should().Be(200);
        (
            await _other.Harness.Room.HandleWiredWebApiAsync(
                new WiredWebApiRequest
                {
                    Operation = WiredWebApiOperation.PatchGlobal,
                    VariableName = "linked",
                    WriteKey = write,
                    Body = """{"value":"9"}""",
                },
                Ct
            )
        ).Should().Be(new WiredWebApiResponse(403, """{"error":"wired.variables.operation_not_allowed"}"""));

        Read(_coins, Key(_coins, WiredVariableTargetType.Global, 0)).Should().Be(5);
    }

    [Fact]
    public async Task A_change_where_it_lives_reaches_the_reference()
    {
        await BuildShopAsync();
        var reference = await AddReferenceAsync(
            _coins.GetVarSnapshot().VariableId,
            readOnly: false
        );

        await SetCoinsAsync(40);

        Read(reference, Key(reference, WiredVariableTargetType.Global, 0)).Should().Be(40);
    }

    [Fact]
    public async Task A_user_variable_is_the_same_players_in_both_rooms()
    {
        await BuildShopAsync();
        (
            await _hp.GiveValueAsync(
                Key(_hp, WiredVariableTargetType.User, PLAYER_INDEX),
                new WiredVariableValue(80)
            )
        )
            .Should()
            .BeTrue();
        var reference = await AddReferenceAsync(_hp.GetVarSnapshot().VariableId, readOnly: false);
        var key = Key(reference, WiredVariableTargetType.User, PLAYER_INDEX);

        reference.GetVarSnapshot().TargetType.Should().Be(WiredVariableTargetType.User);
        Read(reference, key).Should().Be(80);

        reference.RemoveValue(key).Should().BeTrue();

        _hp.TryGetValue(Key(_hp, WiredVariableTargetType.User, PLAYER_INDEX), out _)
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task A_read_only_reference_changes_nothing()
    {
        await BuildShopAsync();
        await SetCoinsAsync(5);
        var reference = await AddReferenceAsync(_coins.GetVarSnapshot().VariableId, readOnly: true);
        var key = Key(reference, WiredVariableTargetType.Global, 0);

        (await reference.SetValueAsync(null!, key, new WiredVariableValue(99))).Should().BeFalse();

        reference.GetVarSnapshot().Flags.Has(WiredVariableFlags.CanWriteValue).Should().BeFalse();
        Read(_coins, Key(_coins, WiredVariableTargetType.Global, 0)).Should().Be(5);
    }

    [Fact]
    public async Task Only_the_boxs_owner_may_set_it_up()
    {
        await BuildShopAsync();
        _other.AddBox<WiredVariableReference>(REFERENCE, 3, 3, "wf_var_reference");
        // Rights in the room let a player save its other boxes, not this one.
        (
            (ISet<Turbo.Primitives.Players.PlayerId>)
                RoomHarness.GetMember(_other.Harness.State, "PlayerIdsWithRights")!
        ).Add((Turbo.Primitives.Players.PlayerId)105);
        RoomHarness.SetMember(
            RoomHarness.GetMember(_other.Harness.State, "RoomSnapshot")!,
            "WiredModifyPermissionMask",
            WiredPermissionFlags.Rights
        );

        (
            await _other.Harness.Room.ApplyWiredUpdateAsync(
                ActionContext.CreateForPlayer((Turbo.Primitives.Players.PlayerId)105, (RoomId)2),
                REFERENCE,
                new UpdateVariableMessage
                {
                    Id = REFERENCE,
                    IntParams = [0],
                    StringParam = "coins",
                    StuffIds = [],
                    StuffIds2 = [],
                    DefinitionSpecifics = [],
                    FurniSources = [],
                    PlayerSources = [],
                    VariableIds = [_coins.GetVarSnapshot().VariableId.ToString()],
                    TypeSpecifics = [],
                },
                Ct
            )
        ).ErrorKey.Should().Be("wiredfurni.error.variables.permissions.1");
    }

    [Fact]
    public async Task A_reference_with_no_variable_picked_is_refused()
    {
        await BuildShopAsync();
        _other.AddBox<WiredVariableReference>(SECOND_REFERENCE, 4, 3, "wf_var_reference");

        // "n" is what the editor sends when nothing is picked.
        (await SaveSecondReferenceAsync("n"))
            .ErrorKey.Should()
            .Be("wiredfurni.error.variables.reference.valid");
        (await SaveSecondReferenceAsync("123456"))
            .ErrorKey.Should()
            .Be("wiredfurni.error.variables.reference.valid");
    }

    [Fact]
    public async Task A_reference_to_a_variable_of_the_same_room_is_refused()
    {
        await BuildShopAsync();
        var local = _other.AddBox<WiredVariableRoom>(LOCAL, 6, 6, "wf_var_room");
        (
            await _other.SaveAsync<UpdateVariableMessage>(
                LOCAL,
                intParams: [(int)WiredAvailabilityType.Shared],
                stringParam: "local"
            )
        )
            .Should()
            .BeTrue();
        await local.LoadWiredAsync(Ct);
        _other.AddBox<WiredVariableReference>(SECOND_REFERENCE, 4, 3, "wf_var_reference");

        (await SaveSecondReferenceAsync(local.GetVarSnapshot().VariableId.ToString()))
            .ErrorKey.Should()
            .Be("wiredfurni.error.variables.reference.same_room");
    }

    [Fact]
    public async Task A_second_reference_to_the_same_variable_is_refused()
    {
        await BuildShopAsync();
        var coinsId = _coins.GetVarSnapshot().VariableId.ToString();
        await AddReferenceAsync(_coins.GetVarSnapshot().VariableId, readOnly: false);
        _other.AddBox<WiredVariableReference>(SECOND_REFERENCE, 4, 3, "wf_var_reference");

        (await SaveSecondReferenceAsync(coinsId))
            .ErrorKey.Should()
            .Be("wiredfurni.error.variables.reference.already_exists");
        (await SaveSecondReferenceAsync(_hp.GetVarSnapshot().VariableId.ToString()))
            .IsSaved.Should()
            .BeTrue();
    }

    private Task<Turbo.Primitives.Rooms.Wired.WiredSaveResult> SaveSecondReferenceAsync(
        string variableId
    ) =>
        _other.SaveWithResultAsync<UpdateVariableMessage>(
            SECOND_REFERENCE,
            intParams: [0],
            stringParam: "linked2",
            variableIds: [variableId]
        );

    private async Task BuildShopAsync()
    {
        _coins = _shop.AddBox<WiredVariableRoom>(COINS, 6, 6, "wf_var_room");
        (
            await _shop.SaveAsync<UpdateVariableMessage>(
                COINS,
                intParams: [(int)WiredAvailabilityType.Shared],
                stringParam: "coins"
            )
        )
            .Should()
            .BeTrue();
        await _coins.LoadWiredAsync(Ct);

        _hp = _shop.AddBox<WiredVariableUser>(HP, 6, 5, "wf_var_user");
        (
            await _shop.SaveAsync<UpdateVariableMessage>(
                HP,
                intParams: [(int)WiredAvailabilityType.Shared, 1],
                stringParam: "hp"
            )
        )
            .Should()
            .BeTrue();
        await _hp.LoadWiredAsync(Ct);
    }

    private async Task SetCoinsAsync(int value) =>
        (
            await _coins.SetValueAsync(
                null!,
                Key(_coins, WiredVariableTargetType.Global, 0),
                new WiredVariableValue(value)
            )
        )
            .Should()
            .BeTrue();

    private async Task<WiredVariableReference> AddReferenceAsync(
        WiredVariableId variableId,
        bool readOnly
    )
    {
        var reference = _other.AddBox<WiredVariableReference>(REFERENCE, 3, 3, "wf_var_reference");

        (
            await _other.SaveAsync<UpdateVariableMessage>(
                REFERENCE,
                intParams: [readOnly ? 1 : 0],
                stringParam: "linked",
                variableIds: [variableId.ToString()]
            )
        )
            .Should()
            .BeTrue();
        await reference.LoadWiredAsync(Ct);

        return reference;
    }

    private static long Read(FurnitureWiredVariableLogic variable, WiredVariableKey key)
    {
        variable.TryGetValue(key, out var value).Should().BeTrue();

        return value.Value;
    }

    private static WiredVariableKey Key(
        FurnitureWiredVariableLogic variable,
        WiredVariableTargetType targetType,
        int targetId
    ) => new(variable.GetVarSnapshot().VariableId, targetType, targetId);
}
