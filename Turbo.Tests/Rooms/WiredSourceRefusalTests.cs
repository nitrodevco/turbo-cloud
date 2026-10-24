using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A bot box that also acts on users has a bots slot ([0,100,200,201], default 100) and a users
/// slot ([0,200,201], default 0). A save naming a source its slot does not offer is refused with
/// <c>wiredfurni.error.invalid_user_source</c>; a save with fewer slots keeps the defaults of the
/// missing ones (official client capture 2026-10-22T22-55-08-UpdateAction / 22-55-31-click).
/// </summary>
public sealed class WiredSourceRefusalTests
{
    private const int BOX = 1;

    private readonly WiredRoom _room = new(8, 8);

    public WiredSourceRefusalTests() =>
        _room.AddBox<WiredActionBotFollowAvatar>(BOX, 0, 0, "wf_act_bot_follow_avatar");

    private WiredActionBotFollowAvatar Box =>
        (WiredActionBotFollowAvatar)_room.FloorItem(BOX).Logic;

    [Theory]
    [InlineData(WiredPlayerSourceType.BotByName, WiredPlayerSourceType.BotByName)]
    [InlineData(WiredPlayerSourceType.AllRoomUsers, WiredPlayerSourceType.TriggeredUser)]
    public async Task A_source_the_slot_does_not_offer_is_refused(
        WiredPlayerSourceType bots,
        WiredPlayerSourceType users
    )
    {
        var result = await SaveAsync([
            [bots],
            [users],
        ]);

        result.IsSaved.Should().BeFalse();
        result.ErrorKey.Should().Be("wiredfurni.error.invalid_user_source");
    }

    [Fact]
    public async Task The_offered_sources_save()
    {
        (
            await SaveAsync([
                [WiredPlayerSourceType.SelectorUsers],
                [WiredPlayerSourceType.SignalUsers],
            ])
        ).IsSaved.Should().BeTrue();

        Box.GetPlayerSources()
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    new[] { WiredPlayerSourceType.SelectorUsers },
                    [WiredPlayerSourceType.SignalUsers],
                },
                o => o.WithStrictOrdering()
            );
    }

    [Fact]
    public async Task Missing_slots_keep_their_defaults()
    {
        (
            await SaveAsync([
                [WiredPlayerSourceType.TriggeredUser],
            ])
        ).IsSaved.Should().BeTrue();

        Box.GetPlayerSources()
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    new[] { WiredPlayerSourceType.TriggeredUser },
                    [WiredPlayerSourceType.TriggeredUser],
                },
                o => o.WithStrictOrdering()
            );

        (await SaveAsync([])).IsSaved.Should().BeTrue();

        Box.GetPlayerSources()
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    new[] { WiredPlayerSourceType.BotByName },
                    [WiredPlayerSourceType.TriggeredUser],
                },
                o => o.WithStrictOrdering()
            );
    }

    private Task<Turbo.Primitives.Rooms.Wired.WiredSaveResult> SaveAsync(
        WiredPlayerSourceType[][] playerSources
    ) =>
        _room.SaveWithResultAsync<UpdateActionMessage>(
            BOX,
            intParams: [0],
            stringParam: "Frank",
            playerSources: playerSources
        );
}
