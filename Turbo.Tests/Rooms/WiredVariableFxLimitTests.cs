using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Habbo shows at most five variable fx per variable box (official client capture 2026-10-20,
/// 2 of 2): a sixth on the same box saves and its config is sent, but it is never drawn, the
/// owner gets a wired.error bubble (room.error.wired.variable_fx_limit) and VARIABLE_FX_LIMIT
/// counts once; fx on another variable box still draw. Turbo drew up to 25 per room.
/// </summary>
public sealed class WiredVariableFxLimitTests
{
    private const int HP = 10;
    private const int OTHER_HP = 11;
    private const int FIRST_FX = 20;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task A_sixth_fx_on_one_variable_box_is_sent_but_not_drawn_and_counted_once()
    {
        var hp = await AddUserVariableAsync(HP, 5, 5, "hp");
        for (var i = 0; i < 6; i++)
            await AddFxAsync(FIRST_FX + i, 5, 5);

        _room.Enter(1, 1, 1);
        await GiveAsync(hp, 40);
        await ProcessAsync();

        Configs().Should().BeEquivalentTo(Enumerable.Range(FIRST_FX, 6));
        DrawnConfigs().Should().BeEquivalentTo(Enumerable.Range(FIRST_FX, 5));
        Wired.GetErrorLogs(_now)[9].ErrorName.Should().Be("VARIABLE_FX_LIMIT");
        Wired.GetErrorLogs(_now)[9].ThrowCount.Should().Be(1);
        Notices().Should().Contain("${room.error.wired.variable_fx_limit}");
    }

    [Fact]
    public async Task Fx_on_another_variable_box_still_draw()
    {
        var hp = await AddUserVariableAsync(HP, 5, 5, "hp");
        var other = await AddUserVariableAsync(OTHER_HP, 6, 6, "mana");
        for (var i = 0; i < 5; i++)
            await AddFxAsync(FIRST_FX + i, 5, 5);
        await AddFxAsync(FIRST_FX + 5, 6, 6);

        _room.Enter(1, 1, 1);
        await GiveAsync(hp, 40);
        await GiveAsync(other, 7);
        await ProcessAsync();

        DrawnConfigs().Should().BeEquivalentTo(Enumerable.Range(FIRST_FX, 6));
        Wired.GetErrorLogs(_now)[9].ThrowCount.Should().Be(0);
    }

    // Wired matrix addon.1200.R1 (wlab4): Give Variable 50, then set 30, 30, 80: every write sent
    // one VariableFxStatus, the unchanged 30 too (official client capture 2026-10-24, 4/4).
    [Fact]
    public async Task Addon_1200_R1_a_value_written_unchanged_is_shown_again()
    {
        var hp = await AddUserVariableAsync(HP, 5, 5, "hp");
        await AddFxAsync(FIRST_FX, 5, 5);

        _room.Enter(1, 1, 1);
        await GiveAsync(hp, 50);
        await ProcessAsync();

        foreach (var value in new[] { 30, 30, 80 })
        {
            var before = Statuses().Count;

            await SetAsync(hp, value);
            await ProcessAsync();

            Statuses()
                .Skip(before)
                .Should()
                .ContainSingle()
                .Which.Statuses.Should()
                .ContainSingle()
                .Which.Value.Should()
                .Be(value);
        }
    }

    // Wired matrix addon.1200.R2 (wlab4): a second hp fx placed on the stack sent its config and a
    // VariableFxStatus {allInitialize true, isInitialize true, 80}; the next change sent ONE
    // VariableFxStatus listing both fx (official client capture 2026-10-24, 2/2).
    [Fact]
    public async Task Addon_1200_R2_a_placed_fx_is_shown_as_a_sync_then_changes_list_every_fx()
    {
        var hp = await AddUserVariableAsync(HP, 5, 5, "hp");
        await AddFxAsync(FIRST_FX, 5, 5);

        _room.Enter(1, 1, 1);
        await GiveAsync(hp, 80);
        await ProcessAsync();

        var before = Statuses().Count;
        await AddFxAsync(FIRST_FX + 1, 5, 5);
        await ProcessAsync();

        var placed = Statuses().Skip(before).Should().ContainSingle().Subject;
        placed.InitializeAll.Should().BeTrue();
        placed.Statuses.Should().ContainSingle().Which.IsInitialize.Should().BeTrue();

        before = Statuses().Count;
        await SetAsync(hp, 60);
        await ProcessAsync();

        Statuses()
            .Skip(before)
            .Should()
            .ContainSingle()
            .Which.Statuses.Select(x => x.Key.ConfigId)
            .Should()
            .BeEquivalentTo([FIRST_FX, FIRST_FX + 1]);
    }

    private List<VariableFxStatusMessageComposer> Statuses() =>
        [.. _room.Harness.Fakes.SentToPlayer<VariableFxStatusMessageComposer>(101)];

    private async Task SetAsync(WiredVariableUser variable, int value) =>
        (
            await variable.SetValueAsync(
                _room.Harness.Fakes.Create<IWiredExecutionContext>(),
                new WiredVariableKey(
                    variable.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.User,
                    1
                ),
                new WiredVariableValue(value)
            )
        )
            .Should()
            .BeTrue();

    private async Task<WiredVariableUser> AddUserVariableAsync(int id, int x, int y, string name)
    {
        var variable = _room.AddBox<WiredVariableUser>(id, x, y, "wf_var_user");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                id,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: name
            )
        )
            .Should()
            .BeTrue();
        await variable.LoadWiredAsync(Ct);

        return variable;
    }

    private async Task AddFxAsync(int id, int x, int y)
    {
        var fx = _room.AddBox<WiredAddonVariableFxHealthPoints>(id, x, y, "wf_xtra_varfx_hp");
        (await _room.SaveAsync<UpdateAddonMessage>(id, intParams: [.. fx.GetDefaultIntParams()]))
            .Should()
            .BeTrue();
        await fx.LoadWiredAsync(Ct);
    }

    private static async Task GiveAsync(WiredVariableUser variable, int value) =>
        (
            await variable.GiveValueAsync(
                new WiredVariableKey(
                    variable.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.User,
                    1
                ),
                new WiredVariableValue(value)
            )
        )
            .Should()
            .BeTrue();

    private async Task ProcessAsync()
    {
        for (var i = 0; i < 3; i++)
        {
            _now += 2_000;
            await _room.Harness.Room.VariableFxSystem.ProcessAsync(_now, Ct);
        }
    }

    private IEnumerable<int> Configs() =>
        _room
            .Harness.Fakes.SentToPlayer<VariableFxConfigsMessageComposer>(101)
            .SelectMany(x => x.Configs)
            .Select(x => x.ConfigId)
            .Distinct();

    private IEnumerable<int> DrawnConfigs() =>
        _room
            .Harness.Fakes.SentToPlayer<VariableFxStatusMessageComposer>(101)
            .SelectMany(x => x.Statuses)
            .Select(x => x.Key.ConfigId)
            .Distinct();

    private IEnumerable<string> Notices() =>
        _room
            .Harness.Fakes.Sent<NotificationDialogMessageComposer>(SendRoute.Direct)
            .Select(x => x.Parameters["message"]);
}
