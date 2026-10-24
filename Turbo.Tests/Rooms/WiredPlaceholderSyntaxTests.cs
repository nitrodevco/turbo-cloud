using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Variables.User;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A text placeholder is written "$(name)": the placeholder editor tells the player so
/// (<c>PlaceholderNameSection</c>, "Use this by typing $(name) in Wired texts"), and the Wired
/// Faculty tutorial "Advanced Automatic Shop" writes its shop message with "$(furni_name)" and
/// "$(price_tag)". The text output add-ons looked for "$name", so a text written the official way
/// kept its placeholder.
/// </summary>
public sealed class WiredPlaceholderSyntaxTests
{
    private const int PLAYER_INDEX = 5;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Hi $(user)!", "Hi Alice!")]
    [InlineData("$(user) and $(user)", "Alice and Alice")]
    [InlineData("Hi $user!", "Hi $user!")]
    public async Task The_username_placeholder_is_written_in_brackets(string text, string shown)
    {
        _room.Enter(PLAYER_INDEX, 1, 1, "Alice");

        var placeholder = _room.AddBox<WiredAddonUsernamePlaceholder>(
            1,
            0,
            0,
            "wf_xtra_text_output_username"
        );

        (await _room.SaveAsync<UpdateAddonMessage>(1, intParams: [0], stringParam: "user"))
            .Should()
            .BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);
        ctx.Policy.TextPlaceholders.Add(placeholder);

        (await ctx.FormatTextAsync(text, Ct)).Should().Be(shown);
    }

    /// <summary>
    /// Wired Faculty tutorial "How to make bot say the name of both the Triggering and the Target
    /// user" (24/05/2025): two username placeholders on one stack, one reading the triggering
    /// user and one the selector's, so "$(triggering) clicked $(target)" names both.
    /// </summary>
    [Fact]
    public async Task Each_placeholder_names_the_users_of_its_own_source()
    {
        _room.Enter(PLAYER_INDEX, 1, 1, "Alice");
        _room.Enter(PLAYER_INDEX + 1, 2, 2, "Bob");

        var triggering = _room.AddBox<WiredAddonUsernamePlaceholder>(
            1,
            0,
            0,
            "wf_xtra_text_output_username"
        );
        var target = _room.AddBox<WiredAddonUsernamePlaceholder>(
            2,
            0,
            0,
            "wf_xtra_text_output_username"
        );

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                1,
                intParams: [0],
                stringParam: "triggering",
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                2,
                intParams: [0],
                stringParam: "target",
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ]
            )
        ).Should().BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);
        ctx.SelectorPool.SelectedAvatarIds.Add(PLAYER_INDEX + 1);
        ctx.Policy.TextPlaceholders.Add(triggering);
        ctx.Policy.TextPlaceholders.Add(target);

        (await ctx.FormatTextAsync("$(triggering) clicked $(target)", Ct))
            .Should()
            .Be("Alice clicked Bob");
    }

    /// <summary>
    /// Wired matrix W3 action.7.R7 / addon.14.R4 / addon.15.R5 / addon.19.R4 (measured, text
    /// substitution and capture): the triggering user's name, a user's numeric variable and the
    /// picked furni's name were all put in one message, and "%name%" stayed as it was written.
    /// </summary>
    [Fact]
    public async Task Username_variable_and_furni_name_fill_one_message_and_percent_stays_literal()
    {
        var avatar = _room.Enter(PLAYER_INDEX, 1, 1, "Alice");

        avatar.SetRotation((Rotation)3);

        var direction = new UserDirectionVariable(_room.Harness.Room);
        var directionId = direction.GetVarSnapshot().VariableId;

        _room.Harness.Room.WiredSystem.VariablesById()[directionId] = direction;
        _room.AddFloorItem(9, 4, 4, className: "throne");

        var user = _room.AddBox<WiredAddonUsernamePlaceholder>(
            1,
            0,
            0,
            "wf_xtra_text_output_username"
        );
        var variable = _room.AddBox<WiredAddonVariablePlaceholder>(
            2,
            0,
            0,
            "wf_xtra_text_output_variable"
        );
        var furni = _room.AddBox<WiredAddonFurniNamePlaceholder>(
            3,
            0,
            0,
            "wf_xtra_text_output_furni_name"
        );

        (await _room.SaveAsync<UpdateAddonMessage>(1, intParams: [0], stringParam: "user"))
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                2,
                intParams: [0, (int)WiredVariableTargetType.User, 0],
                stringParam: "dir",
                variableIds: [directionId.ToString()]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                3,
                intParams: [0],
                stringParam: "furni",
                stuffIds: [9],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);
        ctx.Policy.TextPlaceholders.Add(user);
        ctx.Policy.TextPlaceholders.Add(variable);
        ctx.Policy.TextPlaceholders.Add(furni);

        (await ctx.FormatTextAsync("$(user) faces $(dir) by $(furni), not %user%", Ct))
            .Should()
            .Be("Alice faces 3 by throne, not %user%");
    }
}
