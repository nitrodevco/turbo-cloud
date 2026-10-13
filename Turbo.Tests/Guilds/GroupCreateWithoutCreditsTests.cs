using FluentAssertions;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Guilds;

/// <summary>
/// Buying a group without the credits for it is answered with NotEnoughBalance, the one reply the
/// Flash client turns into the not-enough-credits alert whether or not the catalog is open
/// (HabboCatalog.as:2331-2337); its group code has no failure of its own for the price
/// (HabboGroupsManager.as:451, no group.edit.fail text). Turbo logged the refusal and sent
/// nothing, so the wizard sat on its buy button. Not recorded for a group.
/// </summary>
public sealed class GroupCreateWithoutCreditsTests
{
    private static byte[] CreateGuild() =>
        PacketHarness.Payload(w =>
            w.String("broke group").String(string.Empty).Int(5).Int(1).Int(1).Int(0)
        );

    [Fact]
    public async Task Too_few_credits_shows_the_not_enough_credits_alert()
    {
        var harness = new PacketHarness();
        harness.Fakes.Handlers["CreateGuildAsync"] = _ =>
            Task.FromResult(
                GuildCreationResultSnapshot.Failed(GuildCreationFailureType.InsufficientCredits)
            );

        await harness.SendAsync(PacketHarness.Incoming("CreateGuildMessageEvent"), CreateGuild());

        harness
            .Sent.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new NotEnoughBalanceMessageComposer
                {
                    NotEnoughCredits = true,
                    NotEnoughActivityPoints = false,
                    ActivityPointType = 0,
                }
            );
    }

    [Fact]
    public async Task A_failure_with_no_text_still_sends_nothing()
    {
        var harness = new PacketHarness();
        harness.Fakes.Handlers["CreateGuildAsync"] = _ =>
            Task.FromResult(
                GuildCreationResultSnapshot.Failed(GuildCreationFailureType.CreationFailed)
            );

        await harness.SendAsync(PacketHarness.Incoming("CreateGuildMessageEvent"), CreateGuild());

        harness.Sent.Should().BeEmpty();
    }
}
