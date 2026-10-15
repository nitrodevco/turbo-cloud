using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A save whose int param is out of what the box takes is refused with the hotel's text for it:
/// a freeze effect the editor does not offer gets <c>wiredfurni.error.invalid_freeze_effect</c>,
/// a hand item past the last one <c>wiredfurni.error.invalid_handitem_type</c>. A param with no
/// text of its own still gets the generic refusal.
/// </summary>
public sealed class WiredInvalidParamErrorTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public async Task A_freeze_effect_the_editor_does_not_offer_is_refused_with_its_text()
    {
        _room.AddBox<WiredActionFreezeUser>(1, 0, 0, "wf_act_freeze");

        (await SaveFreezeAsync(5, 0))
            .ErrorKey.Should()
            .Be("wiredfurni.error.invalid_freeze_effect");
        (await SaveFreezeAsync(-1, 0))
            .ErrorKey.Should()
            .Be("wiredfurni.error.invalid_freeze_effect");
        (await SaveFreezeAsync(4, 2)).ErrorKey.Should().BeNull();
        (await SaveFreezeAsync(4, 0)).IsSaved.Should().BeTrue();
    }

    [Fact]
    public async Task A_hand_item_past_the_last_is_refused_with_its_text()
    {
        _room.AddBox<WiredConditionHabboHasHanditem>(1, 0, 0, "wf_cnd_has_handitem");
        _room.AddBox<WiredSelectorEntitiesWithHanditem>(2, 1, 0, "wf_slc_users_handitem");

        (await _room.SaveWithResultAsync<UpdateConditionMessage>(1, intParams: [10_001]))
            .ErrorKey.Should()
            .Be("wiredfurni.error.invalid_handitem_type");
        (await _room.SaveWithResultAsync<UpdateSelectorMessage>(2, intParams: [10_001]))
            .ErrorKey.Should()
            .Be("wiredfurni.error.invalid_handitem_type");
        (await _room.SaveWithResultAsync<UpdateConditionMessage>(1, intParams: [7]))
            .IsSaved.Should()
            .BeTrue();
    }

    private Task<Turbo.Primitives.Rooms.Wired.WiredSaveResult> SaveFreezeAsync(
        int effect,
        int cancelOnTeleport
    ) =>
        _room.SaveWithResultAsync<UpdateActionMessage>(
            1,
            intParams: [effect, cancelOnTeleport],
            playerSources:
            [
                [WiredPlayerSourceType.TriggeredUser],
            ],
            definitionSpecifics: [0]
        );
}
