using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A variable box's name is refused with the text Habbo sends, not cut or let through: an empty
/// name gets <c>wiredfurni.error.variables.name_length</c> (official client capture
/// session-20261012-102507 and -181511: UpdateVariable with an empty name), a name of other
/// characters than letters, digits and "_" <c>name_syntax</c> (the client's own rule,
/// LinkDataframeCodec.keyError, AS3 WIN63-202610131318), a name another variable of the room has
/// <c>name_uniq</c> and a permanent variable over the room's cap <c>permanent_limit</c>
/// (official client capture 2026-10-11). Ours saved any name, cut to the string limit, and
/// answered the cap with the generic error.
/// </summary>
public sealed class WiredVariableNameRulesTests
{
    private const int FURNI_VAR = 2;
    private const int CONTEXT_VAR = 3;
    private const int OTHER_FURNI_VAR = 4;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("", WiredSaveErrors.VARIABLE_NAME_LENGTH)]
    [InlineData("a_name_of_forty_one_characters_is_too_lon", WiredSaveErrors.VARIABLE_NAME_LENGTH)]
    [InlineData("two words", WiredSaveErrors.VARIABLE_NAME_SYNTAX)]
    [InlineData("pontuação", WiredSaveErrors.VARIABLE_NAME_SYNTAX)]
    [InlineData("score.total", WiredSaveErrors.VARIABLE_NAME_SYNTAX)]
    public async Task A_name_outside_the_rule_is_refused_with_Habbos_text(string name, string key)
    {
        _room.AddBox<WiredVariableFurni>(FURNI_VAR, 0, 0, "wf_var_furni");

        (await SaveFurniAsync(FURNI_VAR, name)).ErrorKey.Should().Be(key);
    }

    [Fact]
    public async Task A_name_of_forty_letters_digits_and_underscores_is_saved()
    {
        _room.AddBox<WiredVariableFurni>(FURNI_VAR, 0, 0, "wf_var_furni");

        (await SaveFurniAsync(FURNI_VAR, "Score_2_" + new string('x', 32)))
            .IsSaved.Should()
            .BeTrue();
    }

    [Fact]
    public async Task A_name_another_variable_of_the_room_has_is_refused_whatever_its_target()
    {
        _room.AddBox<WiredVariableContext>(CONTEXT_VAR, 1, 0, "wf_var_context");
        _room.AddBox<WiredVariableFurni>(FURNI_VAR, 0, 0, "wf_var_furni");

        (
            await _room.SaveWithResultAsync<UpdateVariableMessage>(
                CONTEXT_VAR,
                intParams: [0],
                stringParam: "points"
            )
        )
            .IsSaved.Should()
            .BeTrue();

        (await SaveFurniAsync(FURNI_VAR, "points"))
            .ErrorKey.Should()
            .Be(WiredSaveErrors.VARIABLE_NAME_UNIQUE);
        (await SaveFurniAsync(FURNI_VAR, "points2")).IsSaved.Should().BeTrue();
        (await SaveFurniAsync(FURNI_VAR, "points2"))
            .IsSaved.Should()
            .BeTrue("a box saved again under its own name keeps it");
    }

    [Fact]
    public async Task A_permanent_variable_over_the_rooms_cap_is_refused_with_permanent_limit()
    {
        _room.Harness.Room.UseWiredConfig( // One furni variable at the default level's cap of 2500.
            new WiredConfig { PermanentFurniVariablesPer10000Cap = 4 }
        );
        var first = _room.AddBox<WiredVariableFurni>(FURNI_VAR, 0, 0, "wf_var_furni");
        _room.AddBox<WiredVariableFurni>(OTHER_FURNI_VAR, 2, 0, "wf_var_furni");

        (await SaveFurniAsync(FURNI_VAR, "kept", WiredAvailabilityType.Persistent))
            .IsSaved.Should()
            .BeTrue();
        _room.Harness.Fakes.Handlers["BuildVariablesForRoom"] = _ => new IWiredVariable[] { first };
        await _room.Harness.Module<RoomWiredSystem>().ProcessWiredAsync(11_000, false, Ct);

        (await SaveFurniAsync(OTHER_FURNI_VAR, "second", WiredAvailabilityType.Persistent))
            .ErrorKey.Should()
            .Be(WiredSaveErrors.VARIABLE_PERMANENT_LIMIT);
    }

    private Task<WiredSaveResult> SaveFurniAsync(
        int boxId,
        string name,
        WiredAvailabilityType availability = WiredAvailabilityType.RoomActive
    ) =>
        _room.SaveWithResultAsync<UpdateVariableMessage>(
            boxId,
            intParams: [0, (int)availability],
            stringParam: name
        );
}
