using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Bot boxes keep the sources they were saved with. What every box's editor is offered is held to
/// Habbo's by WiredOfficialEditorContractTests.
/// </summary>
public sealed class WiredOfficialSourcesTests
{
    private readonly WiredRoom _room = new(8, 8);

    /// <summary>
    /// A bot box stacked with Bot Reached Avatar also offers "The reached user" (10): Habbo's seven
    /// bot boxes did on one pile with Bot Reached Avatar and Bot Reached Furni (official client
    /// capture 2026-10-24T14-21-24, Bot Talk [[0,10,100,200,201]]) and did not once moved off it
    /// (14-32-32, [[0,100,200,201]]). Which of the two triggers names the user is inference.
    /// </summary>
    [Fact]
    [Trait("evidence", "inferred")]
    public void A_bot_box_on_a_bot_reached_avatar_stack_offers_the_reached_user()
    {
        var talk = _room.AddBox<WiredActionBotTalk>(1, 0, 0, "wf_act_bot_talk");
        _room.AddBox<WiredTriggerBotReachesHabbo>(2, 0, 0, "wf_trg_bot_reached_avtr");

        talk.GetOfferedPlayerSources()
            .Single()
            .Select(x => (int)WiredPlayerSourceTypeExtensions.GetProtocolId(x))
            .Order()
            .Should()
            .Equal(0, 10, 100, 200, 201);
    }

    /// <summary>
    /// "Use the triggering user" on Bot Talk with no bot name and no message saves (official client
    /// capture: UpdateAction ints [0,-1,0], string "\t", user sources [0] -> WiredSaveSuccess).
    /// </summary>
    [Fact]
    public async Task Bot_talk_saves_with_the_triggering_user()
    {
        var talk = _room.AddBox<WiredActionBotTalk>(1, 0, 0, "wf_act_bot_talk");

        (
            await _room.SaveAsync<Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.UpdateActionMessage>(
                1,
                intParams: [0, -1, 0],
                stringParam: "\t",
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        talk.GetPlayerSources().Single().Should().Equal(WiredPlayerSourceType.TriggeredUser);
    }

    /// <summary>
    /// A follow, hand item or talk to box saved while the users came first and the bot second
    /// loads with the bot in slot 0 and the users in slot 1, so it keeps doing what it did.
    /// </summary>
    [Fact]
    public async Task A_bot_box_saved_with_the_users_first_keeps_its_bot_and_users()
    {
        var follow = _room.AddBox<WiredActionBotFollowAvatar>(1, 0, 0, "wf_act_bot_follow_avatar");

        _room
            .FloorItem(1)
            .ExtraData.UpdateSection(
                Turbo.Primitives.Furniture.Enums.ExtraDataSectionType.WIRED,
                System.Text.Json.JsonSerializer.SerializeToNode(
                    new Turbo.Rooms.Wired.WiredData
                    {
                        StringParam = "Frank",
                        PlayerSources =
                        [
                            [WiredPlayerSourceType.SelectorUsers],
                            [WiredPlayerSourceType.BotByName],
                        ],
                    }
                )!
            );
        await follow.LoadWiredAsync(TestContext.Current.CancellationToken);

        follow
            .GetPlayerSources()
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    new[] { WiredPlayerSourceType.BotByName },
                    [WiredPlayerSourceType.SelectorUsers],
                },
                o => o.WithStrictOrdering()
            );
    }
}
