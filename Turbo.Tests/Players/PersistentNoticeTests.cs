using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Players.Configuration;
using Turbo.Players.Notifications;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// The hotel's persistent notifications (the wired chests' ones): an online player gets them at
/// once; an offline one gets them at the next login, oldest first, at most the newest five ("up
/// to a maximum of 5", the chest notification help), and only once.
/// </summary>
public class PersistentNoticeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const int PLAYER = 3;

    [Fact]
    public async Task An_online_player_gets_it_at_once_and_nothing_is_kept()
    {
        var fakes = new Fakes();
        var db = new InMemoryDb();
        fakes.Handlers["TrySendComposerAsync"] = _ => Task.FromResult(true);
        var notices = Create(fakes, db);

        var delivery = await notices.SendPersistentAsync(
            PLAYER,
            "wired_chests.donation",
            Params(1),
            Ct
        );

        delivery.Should().Be(PlayerNoticeDelivery.Sent);
        fakes
            .Log.Of("TrySendComposerAsync")
            .Select(x => x.Args.OfType<NotificationDialogMessageComposer>().Single())
            .Should()
            .ContainSingle(x => x.NotificationType == "wired_chests.donation");
        await using var ctx = await db.CreateDbContextAsync(Ct);
        ctx.PlayerPendingNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task An_offline_player_gets_the_newest_five_at_login_oldest_first_once()
    {
        var fakes = new Fakes();
        var db = new InMemoryDb();
        var notices = Create(fakes, db);

        for (var i = 1; i <= 7; i++)
            (await notices.SendPersistentAsync(PLAYER, "wired_chests.donation", Params(i), Ct))
                .Should()
                .Be(PlayerNoticeDelivery.Offline);

        await notices.DeliverPersistentAsync(PLAYER, Ct);
        await notices.DeliverPersistentAsync(PLAYER, Ct);

        Delivered(fakes)
            .Select(x => x.Parameters["user_name"])
            .Should()
            .Equal("user3", "user4", "user5", "user6", "user7");
        Delivered(fakes).Should().OnlyContain(x => x.NotificationType == "wired_chests.donation");
    }

    private static List<NotificationDialogMessageComposer> Delivered(Fakes fakes) =>
        fakes
            .Log.Of("SendComposerAsync")
            .SelectMany(x => x.Args.OfType<NotificationDialogMessageComposer>())
            .ToList();

    private static ImmutableDictionary<string, string> Params(int i) =>
        ImmutableDictionary<string, string>
            .Empty.Add("chest_name", "Bank")
            .Add("user_name", $"user{i}");

    private static PlayerNoticeService Create(Fakes fakes, InMemoryDb db) =>
        new(
            fakes.Create<IGrainFactory>(),
            db,
            fakes.Create<IHotelTextProvider>(),
            Options.Create(new PlayerConfig()),
            new CapturingLogger<IPlayerNoticeService>()
        );
}
