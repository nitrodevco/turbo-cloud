using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Official client capture 2026-10-14 (NOTES-iv-save-errors, a "says" stack with two capturers):
/// the same "#(name)" marker twice in the trigger's keyword is refused with
/// <c>wiredfurni.error.capturer.duplicate</c> when the trigger is saved, in any match mode; a
/// second capturer on a context variable another capturer of the stack writes is refused with
/// <c>wiredfurni.error.link.destination_conflict</c> whatever its name, and two capturers with
/// one name on different variables are saved. Ours saved all of these.
/// </summary>
public sealed class WiredCaptureSaveErrorsTests
{
    private const int TRIGGER = 2;
    private const int CAPTURER = 3;
    private const int OTHER_CAPTURER = 4;
    private const int CONTEXT = 5;
    private const int OTHER_CONTEXT = 6;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("#(a) #(a)", 0, false)]
    [InlineData("#(a) x #(a)", 0, false)]
    [InlineData("#(a) #(a)", 1, false)]
    [InlineData("#(a) #(b)", 0, true)]
    [InlineData("#(a)", 0, true)]
    public async Task A_keyword_with_one_marker_twice_is_refused(
        string keyword,
        int mode,
        bool saved
    )
    {
        _room.AddBox<WiredTriggerHabboSaysKeyword>(TRIGGER, 0, 0, "wf_trg_says_something");

        var result = await _room.SaveWithResultAsync<UpdateTriggerMessage>(
            TRIGGER,
            intParams: [0, mode, 0],
            stringParam: keyword
        );

        result.IsSaved.Should().Be(saved);

        if (!saved)
            result.ErrorKey.Should().Be("wiredfurni.error.capturer.duplicate");
    }

    [Fact]
    public async Task A_second_capturer_on_the_same_variable_is_refused_whatever_its_name()
    {
        var (first, second) = await ContextVariablesAsync();
        _room.AddBox<WiredAddonVariableCapturer>(CAPTURER, 0, 0, "wf_xtra_text_input_variable");
        _room.AddBox<WiredAddonVariableCapturer>(
            OTHER_CAPTURER,
            0,
            0,
            "wf_xtra_text_input_variable"
        );

        (await SaveCapturerAsync(CAPTURER, "a", first)).IsSaved.Should().BeTrue();

        (await SaveCapturerAsync(OTHER_CAPTURER, "a", first))
            .ErrorKey.Should()
            .Be("wiredfurni.error.link.destination_conflict");
        (await SaveCapturerAsync(OTHER_CAPTURER, "b", first))
            .ErrorKey.Should()
            .Be("wiredfurni.error.link.destination_conflict");
        (await SaveCapturerAsync(OTHER_CAPTURER, "a", second)).IsSaved.Should().BeTrue();
    }

    private Task<Turbo.Primitives.Rooms.Wired.WiredSaveResult> SaveCapturerAsync(
        int boxId,
        string name,
        string variableId
    ) =>
        _room.SaveWithResultAsync<UpdateAddonMessage>(
            boxId,
            intParams: [0],
            stringParam: name,
            variableIds: [variableId]
        );

    private async Task<(string, string)> ContextVariablesAsync()
    {
        var first = _room.AddBox<WiredVariableContext>(CONTEXT, 0, 0, "wf_var_context");
        var second = _room.AddBox<WiredVariableContext>(OTHER_CONTEXT, 0, 0, "wf_var_context");
        (
            await _room.SaveWithResultAsync<UpdateVariableMessage>(
                CONTEXT,
                intParams: [0],
                stringParam: "ctx"
            )
        )
            .IsSaved.Should()
            .BeTrue();
        (
            await _room.SaveWithResultAsync<UpdateVariableMessage>(
                OTHER_CONTEXT,
                intParams: [0],
                stringParam: "ctx2"
            )
        )
            .IsSaved.Should()
            .BeTrue();
        _room.Harness.Fakes.Handlers["BuildVariablesForRoom"] = _ =>
            new IWiredVariable[] { first, second };
        await _room.Harness.Module<RoomWiredSystem>().ProcessWiredAsync(11_000, false, Ct);

        return (
            first.GetVarSnapshot().VariableId.ToString(),
            second.GetVarSnapshot().VariableId.ToString()
        );
    }
}
