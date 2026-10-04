using System.Collections.Immutable;
using FluentAssertions;
using Orleans;
using Turbo.Admin.Links;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Who may be given a link that makes an admin panel passkey. A link is the account, so nobody
/// gets their own after the first, and nobody gets one for an account that can do more than
/// theirs.
/// </summary>
public sealed class AdminLinkPolicyTests
{
    private static readonly PlayerId OWNER = new(1);
    private static readonly PlayerId MODERATOR = new(2);
    private static readonly PlayerId HELPER = new(3);
    private static readonly PlayerId NEWCOMER = new(4);

    private readonly Fakes _fakes = new();
    private readonly Dictionary<long, string[]> _nodes = new()
    {
        [OWNER.Value] =
        [
            PermissionNodes.Admin.PANEL,
            PermissionNodes.Admin.PASSKEYS_RESET,
            PermissionNodes.Command.SHUTDOWN,
            "command.ban",
        ],
        [MODERATOR.Value] =
        [
            PermissionNodes.Admin.PANEL,
            PermissionNodes.Admin.PASSKEYS_RESET,
            "command.ban",
        ],
        [HELPER.Value] = [PermissionNodes.Admin.PANEL],
        [NEWCOMER.Value] = [PermissionNodes.Admin.PANEL],
    };
    private readonly HashSet<long> _withPasskeys = [OWNER.Value, MODERATOR.Value, HELPER.Value];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminLinkPolicyTests()
    {
        _fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(_nodes[Key(call)].Contains((string)call.Args[0]!));
        _fakes.Handlers["GetResolvedAsync"] = call =>
            Task.FromResult(
                ResolvedPermissionsSnapshot.EMPTY with
                {
                    Granted = [.. _nodes[Key(call)]],
                }
            );
        _fakes.Handlers["GetAccountAsync"] = call =>
            Task.FromResult(
                new AdminAccountSnapshot
                {
                    Passkeys = _withPasskeys.Contains(Key(call))
                        ?
                        [
                            new AdminPasskeySnapshot
                            {
                                Id = 1,
                                Name = "Laptop",
                                CreatedAtUtc = DateTime.UtcNow,
                            },
                        ]
                        : ImmutableArray<AdminPasskeySnapshot>.Empty,
                }
            );
    }

    [Fact]
    public async Task TheConsoleMayGiveAnybodyALink()
    {
        var decision = await Policy().CheckAsync(null, OWNER, Ct);

        decision.IsAllowed.Should().BeTrue();
        decision.ReplacesExisting.Should().BeTrue();
    }

    [Fact]
    public async Task APlayerMayMakeTheirFirstPasskeyThemselves()
    {
        var decision = await Policy().CheckAsync(NEWCOMER, NEWCOMER, Ct);

        decision.IsAllowed.Should().BeTrue();
        decision.ReplacesExisting.Should().BeFalse();
    }

    [Fact]
    public async Task APlayerWithAPasskeyCannotReplaceItThemselves()
    {
        // Not even with the reset node: there is no recovering one's own account.
        var decision = await Policy().CheckAsync(MODERATOR, MODERATOR, Ct);

        decision.Refusal.Should().Be(AdminLinkRefusal.AlreadySetUp);
    }

    [Fact]
    public async Task ALinkForSomebodyElseNeedsTheResetNode()
    {
        var decision = await Policy().CheckAsync(HELPER, NEWCOMER, Ct);

        decision.Refusal.Should().Be(AdminLinkRefusal.NeedsResetNode);
    }

    [Fact]
    public async Task AnAdminMayResetSomebodyWhoCanDoNoMoreThanThey()
    {
        var decision = await Policy().CheckAsync(MODERATOR, HELPER, Ct);

        decision.IsAllowed.Should().BeTrue();
        decision.ReplacesExisting.Should().BeTrue();
    }

    [Fact]
    public async Task AnAdminCannotTakeOverAnAccountThatCanDoMore()
    {
        // The moderator holds the reset node, but the owner can shut the hotel down.
        var decision = await Policy().CheckAsync(MODERATOR, OWNER, Ct);

        decision.Refusal.Should().Be(AdminLinkRefusal.OutranksIssuer);
    }

    private AdminLinkPolicy Policy() => new(_fakes.Create<IGrainFactory>());

    private static long Key(FakeCall call) => Convert.ToInt64(call.Key);
}
