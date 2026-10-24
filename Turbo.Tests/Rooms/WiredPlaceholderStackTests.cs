using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The text output add-ons on one stack (W3 lab, official client capture 2026-10-24): a second
/// placeholder of the same kind and name is refused at save, a same-name pair that got onto one
/// stack anyway answers with the higher item id, and Furni Name names the picked wall items, after
/// the floor ones. Ours saved the second one, let the first in the stack answer, and named floor
/// furni only.
/// </summary>
public sealed class WiredPlaceholderStackTests
{
    private const int PLAYER_INDEX = 5;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // addon.14.R2 / addon.19.R2: saved into a stack that already has one of that kind and name,
    // the save is refused with wiredfurni.error.link.destination_conflict (3/3); another name, or
    // the same name on another tile, saves.
    [Theory]
    [InlineData("u", 0, false)]
    [InlineData("other", 0, true)]
    [InlineData("u", 1, true)]
    public async Task A_second_placeholder_of_one_name_on_a_stack_is_refused_addon_14_19_R2(
        string name,
        int y,
        bool saves
    )
    {
        _room.AddBox<WiredAddonUsernamePlaceholder>(1, 0, 0, "wf_xtra_text_output_username");
        _room.AddBox<WiredAddonUsernamePlaceholder>(2, 0, y, "wf_xtra_text_output_username");
        _room.AddBox<WiredAddonFurniNamePlaceholder>(3, 1, 0, "wf_xtra_text_output_furni_name");
        _room.AddBox<WiredAddonFurniNamePlaceholder>(4, 1, y, "wf_xtra_text_output_furni_name");

        (await _room.SaveAsync<UpdateAddonMessage>(1, intParams: [0], stringParam: "u"))
            .Should()
            .BeTrue();
        (await _room.SaveAsync<UpdateAddonMessage>(3, intParams: [0], stringParam: "u"))
            .Should()
            .BeTrue();

        var user = await _room.SaveWithResultAsync<UpdateAddonMessage>(
            2,
            intParams: [0],
            stringParam: name
        );
        var furni = await _room.SaveWithResultAsync<UpdateAddonMessage>(
            4,
            intParams: [0],
            stringParam: name
        );

        user.IsSaved.Should().Be(saves);
        furni.IsSaved.Should().Be(saves);

        if (!saves)
            user.ErrorKey.Should().Be("wiredfurni.error.link.destination_conflict");
    }

    // addon.14.X5: two "u" placeholders saved apart and then moved onto one stack by hand (no
    // save, no check): the one with the higher item id answers, whatever their order (4/4).
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Of_two_placeholders_with_one_name_the_higher_item_id_answers_addon_14_X5(
        bool higherFirst
    )
    {
        _room.Enter(PLAYER_INDEX, 1, 1, "Alice");
        _room.Enter(PLAYER_INDEX + 1, 2, 2, "Bob");

        var lower = _room.AddBox<WiredAddonUsernamePlaceholder>(
            128,
            0,
            0,
            "wf_xtra_text_output_username"
        );
        var higher = _room.AddBox<WiredAddonUsernamePlaceholder>(
            129,
            0,
            1,
            "wf_xtra_text_output_username"
        );

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                128,
                intParams: [0],
                stringParam: "u",
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                129,
                intParams: [0],
                stringParam: "u",
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ]
            )
        ).Should().BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);
        ctx.SelectorPool.SelectedAvatarIds.Add(PLAYER_INDEX + 1);
        ctx.Policy.TextPlaceholders.AddRange(higherFirst ? [higher, lower] : [lower, higher]);

        (await ctx.FormatTextAsync("U=$(u)", Ct)).Should().Be("U=Bob");
    }

    // addon.19.X2: multi "fw\t," picking a wall item, then a floor lamp: "W=Pura lamp,Blue Clock"
    // (2/2): the wall item is named, after the floor furni although it was picked first.
    // addon.19.B4: a wall item sent with its positive id (a crafted packet) is saved without it
    // (2/2).
    [Fact]
    public async Task Furni_name_names_picked_wall_items_after_the_floor_ones_addon_19_X2_B4()
    {
        _room.AddFloorItem(9, 4, 4, className: "lamp");
        _room.AddWallItem(50);

        var furni = _room.AddBox<WiredAddonFurniNamePlaceholder>(
            1,
            0,
            0,
            "wf_xtra_text_output_furni_name"
        );

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                1,
                intParams: [1],
                stringParam: "fw\t,",
                stuffIds: [-50, 9],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Policy.TextPlaceholders.Add(furni);

        (await ctx.FormatTextAsync("W=$(fw) end", Ct)).Should().Be("W=lamp,wall_lamp end");

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                1,
                intParams: [1],
                stringParam: "fw\t,",
                stuffIds: [50],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        furni.GetStuffIds().Should().BeEmpty();
    }

    // addon.19.B4: with no furni, "show all" prints nothing ("W= end") and a single name leaves the
    // placeholder as written ("W=$(fw) end"), 2/2 each. Ours printed nothing for both.
    [Theory]
    [InlineData(1, "W= end")]
    [InlineData(0, "W=$(fw) end")]
    public async Task Furni_name_with_no_furni_prints_nothing_or_stays_as_written_addon_19_B4(
        int showsAll,
        string shown
    )
    {
        var furni = _room.AddBox<WiredAddonFurniNamePlaceholder>(
            1,
            0,
            0,
            "wf_xtra_text_output_furni_name"
        );

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                1,
                intParams: [showsAll],
                stringParam: "fw\t,",
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Policy.TextPlaceholders.Add(furni);

        (await ctx.FormatTextAsync("W=$(fw) end", Ct)).Should().Be(shown);
    }
}
