using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Authentication;
using Turbo.Players.Accounts;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Accounts;
using Turbo.Tests.Commands;
using Turbo.Tests.Players;
using Turbo.Tests.Support;
using Turbo.Web.Commands;
using Turbo.Web.Configuration;
using Xunit;

namespace Turbo.Tests.Web;

/// <summary>
/// <c>createplayer</c> from the server console: how a hotel without the public site or Discord
/// gets its first player. The player is made, and the ticket it prints signs them in once.
/// </summary>
public sealed class CreatePlayerCommandTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private OperatorFixture _hotel = null!;

    public CreatePlayerCommandTests() => Register(clientUrl: "");

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task The_console_makes_a_player_whose_printed_ticket_signs_them_in_once()
    {
        var console = new FakeExecutor(null, "console");

        (await _hotel.RunAsync("createplayer", console, "Josh"))
            .Should()
            .Be(CommandOutcome.Completed);

        var ticket = console.Notices.Should().ContainSingle().Which[^1];
        var login = new AuthenticationService(_db, TimeProvider.System);

        var player = await login.GetPlayerIdFromTicketAsync(
            ticket,
            TestContext.Current.CancellationToken
        );

        player.Should().BePositive();
        (await login.GetPlayerIdFromTicketAsync(ticket, TestContext.Current.CancellationToken))
            .Should()
            .Be(0, "the ticket works once");
    }

    [Fact]
    public async Task With_a_client_url_the_ticket_comes_as_a_link_to_the_client()
    {
        Register(clientUrl: "https://play.example/?sso={ticket}");
        var console = new FakeExecutor(null, "console");

        await _hotel.RunAsync("createplayer", console, "Josh female");

        console
            .Notices.Should()
            .ContainSingle()
            .Which[^1]
            .Should()
            .StartWith("https://play.example/?sso=");
    }

    [Fact]
    public async Task A_name_that_is_taken_makes_no_player_and_says_why()
    {
        var console = new FakeExecutor(null, "console");
        await _hotel.RunAsync("createplayer", console, "Josh");

        (await _hotel.RunAsync("createplayer", console, "josh"))
            .Should()
            .NotBe(CommandOutcome.Completed);
        console.Replies.Should().Contain(x => x.Contains("already a player called"));
    }

    private void Register(string clientUrl)
    {
        _hotel = new OperatorFixture();
        _hotel.Commands.Register([
            new CreatePlayerCommand(
                new PlayerAccountService(
                    _db,
                    new NoOwner(),
                    new PassThroughWordFilter(),
                    Options.Create(new Turbo.Players.Configuration.OnboardingConfig()),
                    new Turbo.Players.Accounts.PlayerNamePolicy(
                        Options.Create(new Turbo.Players.Configuration.PlayerConfig())
                    ),
                    NullLogger<IPlayerAccountService>.Instance
                ),
                new LoginTicketService(_db, TimeProvider.System),
                Options.Create(new WebConfig { ClientUrl = clientUrl })
            ),
        ]);
    }

    private sealed class NoOwner : IOwnerBootstrap
    {
        public Task<bool> PlayerCreatedAsync(PlayerId player, string name, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<bool> DiscordLinkedAsync(
            PlayerId player,
            string discordId,
            CancellationToken ct
        ) => Task.FromResult(false);
    }
}
