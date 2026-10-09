using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Handshake;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Hotel.Grains;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Settings;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The welcome message staff set in the admin panel: every login is shown it as the message of
/// the day, last, and a login while it is empty is shown nothing.
/// </summary>
public class WelcomeMessageLoginTests
{
    private const int PLAYER = 5;

    private readonly Fakes _fakes = new();
    private readonly ISessionContext _session;
    private readonly SSOTicketMessageHandler _handler;
    private string _welcome = string.Empty;

    public WelcomeMessageLoginTests()
    {
        _fakes.Handlers["GetPlayerIdFromTicketAsync"] = _ => Task.FromResult(PLAYER);
        _fakes.Handlers["GetOwnedAsync"] = _ =>
            Task.FromResult(System.Collections.Immutable.ImmutableHashSet<int>.Empty);
        _fakes.Handlers["AdmitsAsync"] = _ => Task.FromResult(true);
        _fakes.Handlers["GetSettingsAsync"] = _ =>
            Task.FromResult(Activator.CreateInstance<PlayerSettingsSnapshot>());
        _fakes.Handlers["GetFavouriteRoomIdsAsync"] = _ =>
            Task.FromResult(ImmutableArray<RoomId>.Empty);
        _fakes.Handlers["GetClubGiftInfoAsync"] = _ => Task.FromResult(ClubGiftInfoSnapshot.Empty);
        _fakes.Handlers["GetUnseenItemsAsync"] = _ =>
            Task.FromResult(ImmutableDictionary<UnseenItemCategory, ImmutableArray<int>>.Empty);
        _fakes.Handlers["GetSummaryAsync"] = _ =>
            Task.FromResult(Activator.CreateInstance<PlayerSummarySnapshot>());
        _fakes.Handlers[nameof(ICallForHelpService.GetTopicsAsync)] = _ => Task.FromResult(TOPICS);
        _fakes.Handlers[nameof(IWelcomeMessageGrain.GetMessageAsync)] = _ =>
            Task.FromResult(_welcome);

        _session = _fakes.Create<ISessionContext>("session");
        _handler = new SSOTicketMessageHandler(
            _fakes.Create<IAuthenticationService>(),
            _fakes.Create<ISessionGateway>(),
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<INavigatorService>(),
            _fakes.Create<ISanctionService>(),
            _fakes.Create<IHotelAvailability>(),
            _fakes.Create<IHotelTextProvider>(),
            _fakes.Create<Turbo.Primitives.Figures.IPlayerClothingService>(),
            _fakes.Create<ICallForHelpService>()
        );
    }

    private static readonly ImmutableArray<CfhCategorySnapshot> TOPICS =
    [
        new()
        {
            Name = "game_interruption",
            Topics =
            [
                new()
                {
                    Id = 22,
                    Name = "flooding",
                    Consequence = "mods_till_logout",
                },
            ],
        },
    ];

    private Task LogInAsync() =>
        _handler
            .HandleAsync(
                new SSOTicketMessage { SSO = "ticket" },
                new MessageContext(_session, 0, -1),
                CancellationToken.None
            )
            .AsTask();

    private List<object?> SentToSession() =>
        [
            .. _fakes
                .Log.On<ISessionContext>()
                .Where(x => x.Method == "SendComposerAsync")
                .Select(x => x.Args[0]),
        ];

    [Fact]
    public async Task TheCallForHelpTopics_AreSentAtLogin()
    {
        await LogInAsync();

        SentToSession()
            .OfType<CfhTopicsInitMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Categories.Should()
            .Equal(TOPICS);
    }

    [Fact]
    public async Task AWelcomeMessage_IsShownAsTheMessageOfTheDay_LastOfTheLogin()
    {
        _welcome = "Welcome to the hotel!\nBe nice.";

        await LogInAsync();

        var sent = SentToSession();
        sent.Last()
            .Should()
            .BeOfType<MOTDNotificationEventMessageComposer>()
            .Which.Messages.Should()
            .Equal("Welcome to the hotel!\nBe nice.");
        sent.OfType<MOTDNotificationEventMessageComposer>().Should().ContainSingle();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WithNoWelcomeMessage_NoneIsShown(string welcome)
    {
        _welcome = welcome;

        await LogInAsync();

        var sent = SentToSession();
        sent.Should().NotBeEmpty();
        sent.OfType<MOTDNotificationEventMessageComposer>().Should().BeEmpty();
    }
}
