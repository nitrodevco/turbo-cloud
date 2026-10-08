using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Wired;
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
        var avatar = _room.Enter(PLAYER_INDEX, 1, 1);

        avatar.GetType().GetProperty("Name")!.SetValue(avatar, "Alice");

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
}
