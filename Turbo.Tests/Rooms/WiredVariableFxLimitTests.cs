using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums.Wired;
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

    private List<IComposer> SentToPlayer() =>
        _room
            .Harness.Fakes.Log.Of("SendComposerAsync")
            .Where(call => Equals(call.Key, 101L))
            .SelectMany(call => call.Args.OfType<IEnumerable<IComposer>>().SelectMany(x => x))
            .ToList();

    private IEnumerable<int> Configs() =>
        SentToPlayer()
            .OfType<VariableFxConfigsMessageComposer>()
            .SelectMany(x => x.Configs)
            .Select(x => x.ConfigId)
            .Distinct();

    private IEnumerable<int> DrawnConfigs() =>
        SentToPlayer()
            .OfType<VariableFxStatusMessageComposer>()
            .SelectMany(x => x.Statuses)
            .Select(x => x.Key.ConfigId)
            .Distinct();

    private IEnumerable<string> Notices() =>
        _room
            .Harness.Fakes.Log.Of("SendComposerAsync")
            .SelectMany(call => call.Args.OfType<NotificationDialogMessageComposer>())
            .Select(x => x.Parameters["message"]);
}
