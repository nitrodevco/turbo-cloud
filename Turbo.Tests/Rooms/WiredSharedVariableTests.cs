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
using Turbo.Rooms.Grains.Systems;
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
        ).IsSaved.Should().BeFalse();
    }

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
