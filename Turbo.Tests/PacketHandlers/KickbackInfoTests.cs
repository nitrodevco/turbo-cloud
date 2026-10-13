using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Users;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The club centre asks for ScrGetKickbackInfo when it opens; Habbo answers (hhus, 2026-10-13, a
/// non-member: no streak, no join date, a 0.1 share, nothing spent, payday in 2880 minutes) and
/// Turbo sent nothing, so the centre had no join date, streak or payday to show.
/// </summary>
public sealed class KickbackInfoTests
{
    private static async Task<ScrSendKickbackInfoMessageComposer> AskAsync(
        PlayerSubscriptionSnapshot club
    )
    {
        var fakes = new Fakes();

        fakes.Handlers["GetAsync"] = _ => Task.FromResult(club);

        await new ScrGetKickbackInfoMessageHandler(
            fakes.Create<IGrainFactory>(),
            Options.Create(new PlayerConfig())
        ).HandleAsync(
            new ScrGetKickbackInfoMessage(),
            new MessageContext(fakes.Create<ISessionContext>(), 1, 7),
            TestContext.Current.CancellationToken
        );

        return Assert.IsType<ScrSendKickbackInfoMessageComposer>(
            fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );
    }

    private static PlayerSubscriptionSnapshot Club(
        int secondsLeft,
        int totalDays,
        DateTime? firstSubscribedAt
    ) =>
        new()
        {
            SubscriptionType = SubscriptionType.HabboClub,
            SecondsLeft = secondsLeft,
            SecondsLeftWithGrace = secondsLeft,
            HasEverBeenMember = firstSubscribedAt is not null,
            TotalDaysSubscribed = totalDays,
            DaysRemaining = (int)Math.Ceiling(secondsLeft / 86400d),
            FurniLimit = 0,
            MaxFurniLimit = 0,
            MinutesSinceLastModified = -1,
            ExpiresAt = null,
            FirstSubscribedAt = firstSubscribedAt,
        };

    [Fact]
    public async Task a_non_member_is_answered_as_habbo_answers_one()
    {
        var packet = PacketHarness.Encode(await AskAsync(Club(0, 0, null)));

        // The official reply, byte for byte.
        Assert.Equal(
            Convert.FromHexString(
                "0000000000003FB999999999999A000000000000000000000000000000000000000000000B40"
            ),
            packet.PopBytes(packet.Remaining)
        );
    }

    [Fact]
    public async Task a_member_is_told_when_they_joined_and_how_long_they_have_been_one()
    {
        var reply = await AskAsync(Club(10 * 86400, 50, new DateTime(2026, 8, 24)));

        reply.FirstSubscriptionDate.Should().Be("2026-08-24");
        reply.CurrentHcStreak.Should().Be(40, "50 days granted, 10 still to run");
    }
}
