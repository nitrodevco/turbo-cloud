using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Commands;
using Turbo.Admin.Configuration;
using Turbo.Admin.Links;
using Turbo.Commands;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Notifications;
using Turbo.Tests.Commands;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// <c>:adminsetup</c> hands over a link. Text in a hotel notice can't be selected, so a player
/// must get one they can click; the console still prints it to copy.
/// </summary>
public sealed class AdminSetupCommandTests
{
    private const string LINK = "https://panel.example/setup#token=secret-token";

    private readonly OperatorFixture _hotel = new OperatorFixture().WithPlayer(1, "Alice");

    public AdminSetupCommandTests()
    {
        _hotel.Fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);
        _hotel.Fakes.Handlers["GetAccountAsync"] = _ =>
            Task.FromResult(new AdminAccountSnapshot { Passkeys = [] });
        _hotel.Fakes.Handlers["CreateSetupTokenAsync"] = _ =>
            Task.FromResult(
                new AdminSetupLinkSnapshot
                {
                    Token = "secret-token",
                    ExpiresAtUtc = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc),
                }
            );
        _hotel.Commands.Register([
            new AdminSetupCommand(
                _hotel.Fakes.Create<IGrainFactory>(),
                new AdminLinkPolicy(_hotel.Fakes.Create<IGrainFactory>()),
                Options.Create(
                    new AdminConfig { Enabled = true, PanelUrl = "https://panel.example/" }
                )
            ),
        ]);
    }

    [Fact]
    public async Task APlayer_GetsTheSetupLink_AsALinkToClick()
    {
        var executor = new PlayerOperatorExecutor(
            _hotel.Fakes.Create<IGrainFactory>(),
            1,
            "Alice",
            9,
            [1],
            _hotel.Fakes.Create<IPlayerNoticeService>(),
            new CapturingLogger<PlayerOperatorExecutor>()
        );

        (
            await _hotel.Runner.RunAsync(
                _hotel.Find("adminsetup"),
                executor,
                "",
                checkNode: true,
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(CommandOutcome.Completed);

        var sent = _hotel
            .Calls<IPlayerPresenceGrain>("SendComposerAsync")
            .Should()
            .ContainSingle()
            .Subject;
        var popup = sent.Args[0].Should().BeOfType<NotificationDialogMessageComposer>().Subject;
        popup.Parameters["display"].Should().Be("POP_UP");
        popup.Parameters["title"].Should().Be("Passkey setup for Alice.");
        popup.Parameters["linkUrl"].Should().Be(LINK);
        popup.Parameters["linkTitle"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TheConsole_GetsTheSetupLink_AsItsLastLine()
    {
        var console = new FakeExecutor(null, "console");

        (await _hotel.RunAsync("adminsetup", console, "Alice"))
            .Should()
            .Be(CommandOutcome.Completed);

        console.Notices.Should().ContainSingle().Which[^1].Should().Be(LINK);
    }
}
