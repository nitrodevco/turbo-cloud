using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The placeholder add-ons' names are held to the rule the client holds placeholders to
/// (LinkDataframeCodec.placeholderError, AS3 WIN63-202610131318: 1..40 characters, /^\w+$/,
/// trigger_text reserved) and "show all" needs a delimiter, each refused with Habbo's
/// <c>wiredfurni.error.placeholder.*</c> text. Ours saved any name and put ", " in for an empty
/// delimiter.
/// </summary>
public sealed class WiredPlaceholderNameRulesTests
{
    private const int USERNAME = 2;
    private const int VARIABLE = 3;
    private const int CAPTURER = 4;

    private readonly WiredRoom _room = new(8, 8);

    public WiredPlaceholderNameRulesTests()
    {
        _room.AddBox<WiredAddonUsernamePlaceholder>(USERNAME, 0, 0, "wf_xtra_text_output_username");
        _room.AddBox<WiredAddonVariablePlaceholder>(VARIABLE, 1, 0, "wf_xtra_text_output_variable");
        _room.AddBox<WiredAddonVariableCapturer>(CAPTURER, 2, 0, "wf_xtra_text_input_variable");
    }

    [Theory]
    [InlineData("", 0, WiredSaveErrors.PLACEHOLDER_NAME_LENGTH)]
    [InlineData(
        "a_placeholder_of_forty_one_characters_xxx",
        0,
        WiredSaveErrors.PLACEHOLDER_NAME_LENGTH
    )]
    [InlineData("first name", 0, WiredSaveErrors.PLACEHOLDER_NAME_SYNTAX)]
    [InlineData("trigger_text", 0, WiredSaveErrors.PLACEHOLDER_RESERVED)]
    [InlineData("names", 1, WiredSaveErrors.PLACEHOLDER_DELIMITER_EMPTY)]
    [InlineData("names\t", 1, WiredSaveErrors.PLACEHOLDER_DELIMITER_EMPTY)]
    public async Task A_username_placeholder_is_refused_with_Habbos_text(
        string stringParam,
        int showAll,
        string key
    ) =>
        (
            await _room.SaveWithResultAsync<UpdateAddonMessage>(
                USERNAME,
                intParams: [showAll],
                stringParam: stringParam
            )
        )
            .ErrorKey.Should()
            .Be(key);

    [Fact]
    public async Task A_valid_placeholder_is_saved_with_or_without_show_all()
    {
        (
            await _room.SaveWithResultAsync<UpdateAddonMessage>(
                USERNAME,
                intParams: [0],
                stringParam: "name"
            )
        )
            .IsSaved.Should()
            .BeTrue();
        (
            await _room.SaveWithResultAsync<UpdateAddonMessage>(
                USERNAME,
                intParams: [1],
                stringParam: "names\t, "
            )
        )
            .IsSaved.Should()
            .BeTrue();
    }

    [Fact]
    public async Task The_variable_placeholder_and_the_capturer_hold_their_names_to_the_same_rule()
    {
        (
            await _room.SaveWithResultAsync<UpdateAddonMessage>(
                VARIABLE,
                intParams: [1, 1, 0],
                stringParam: "score"
            )
        )
            .ErrorKey.Should()
            .Be(WiredSaveErrors.PLACEHOLDER_DELIMITER_EMPTY);
        (
            await _room.SaveWithResultAsync<UpdateAddonMessage>(
                CAPTURER,
                intParams: [0],
                stringParam: "my-word"
            )
        )
            .ErrorKey.Should()
            .Be(WiredSaveErrors.PLACEHOLDER_NAME_SYNTAX);
    }
}
