using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Configuration;
using Turbo.Admin.Links;
using Turbo.Events.Registry;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Events;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The hotel's owner gets their admin panel setup link in the server log, so a hotel with no
/// terminal to attach to can still get its first admin in. Only when the panel is on and the owner
/// has no passkey: one who has signed in before is left alone.
/// </summary>
public sealed class OwnerSetupLinkHandlerTests
{
    private readonly Fakes _fakes = new();
    private readonly CapturingLogger<OwnerSetupLinkHandler> _log = new();
    private bool _hasPasskey;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly OwnerConfirmedEvent OWNER = new()
    {
        PlayerId = new PlayerId(1),
        Name = "Alice",
        Granted = true,
    };

    public OwnerSetupLinkHandlerTests()
    {
        _fakes.Handlers["GetAccountAsync"] = _ =>
            Task.FromResult(
                new AdminAccountSnapshot
                {
                    Passkeys = _hasPasskey
                        ?
                        [
                            new AdminPasskeySnapshot
                            {
                                Id = 1,
                                Name = "phone",
                                CreatedAtUtc = DateTime.UtcNow,
                            },
                        ]
                        : [],
                }
            );
        _fakes.Handlers["CreateSetupTokenAsync"] = _ =>
            Task.FromResult(
                new AdminSetupLinkSnapshot
                {
                    Token = "secret-token",
                    ExpiresAtUtc = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc),
                }
            );
    }

    [Fact]
    public async Task AnOwnerWithNoPasskey_IsGivenTheirLinkInTheLog()
    {
        await Handler(enabled: true).HandleAsync(OWNER, new EventContext(), Ct);

        var entry = _log.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Message.Should().Contain("https://panel.example/setup#token=secret-token");
        entry.Message.Should().Contain("Alice");
    }

    [Fact]
    public async Task AnOwnerWhoHasAPasskey_IsLeftAlone()
    {
        _hasPasskey = true;

        await Handler(enabled: true).HandleAsync(OWNER, new EventContext(), Ct);

        _log.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task WithThePanelOff_NoLinkIsMade()
    {
        await Handler(enabled: false).HandleAsync(OWNER, new EventContext(), Ct);

        _log.Entries.Should().BeEmpty();
    }

    private OwnerSetupLinkHandler Handler(bool enabled) =>
        new(
            new AdminLinkPolicy(_fakes.Create<IGrainFactory>()),
            _fakes.Create<IGrainFactory>(),
            Options.Create(
                new AdminConfig { Enabled = enabled, PanelUrl = "https://panel.example/" }
            ),
            _log
        );
}
