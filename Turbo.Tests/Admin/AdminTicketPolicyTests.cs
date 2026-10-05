using FluentAssertions;
using Turbo.Admin.Players;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Who may be given a login ticket, by whom. A ticket logs in as the player, so staff issue them
/// only for players whose every power they have themselves, and for their own account.
/// </summary>
public sealed class AdminTicketPolicyTests
{
    private static readonly PlayerId OWNER = new(1);
    private static readonly PlayerId MODERATOR = new(2);
    private static readonly PlayerId PLAYER = new(3);

    private readonly Fakes _fakes = new();
    private readonly Dictionary<long, string[]> _nodes = new()
    {
        [OWNER.Value] = [PermissionNodes.Admin.TICKETS_ISSUE, "command.shutdown", "command.ban"],
        [MODERATOR.Value] = [PermissionNodes.Admin.TICKETS_ISSUE, "command.ban"],
        [PLAYER.Value] = ["command.ban"],
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminTicketPolicyTests() =>
        _fakes.Handlers["GetResolvedAsync"] = call =>
            Task.FromResult(
                ResolvedPermissionsSnapshot.EMPTY with
                {
                    Granted = [.. _nodes[Convert.ToInt64(call.Key)]],
                }
            );

    private Task<string?> RefusalAsync(PlayerId issuer, PlayerId player) =>
        new AdminTicketPolicy(_fakes.Create<Orleans.IGrainFactory>()).RefusalAsync(
            issuer,
            player,
            Ct
        );

    [Fact]
    public async Task StaffMayIssueOne_ForAPlayerTheyCanDoEverythingOf()
    {
        (await RefusalAsync(MODERATOR, PLAYER)).Should().BeNull();
        (await RefusalAsync(OWNER, MODERATOR)).Should().BeNull();
        (await RefusalAsync(MODERATOR, MODERATOR)).Should().BeNull("their own account");
    }

    [Fact]
    public async Task NotForSomeoneWhoCanDoMore()
    {
        (await RefusalAsync(MODERATOR, OWNER)).Should().Contain("can do things you can't");
        (
            await new AdminTicketPolicy(_fakes.Create<Orleans.IGrainFactory>()).RefusalAsync(
                MODERATOR,
                OWNER,
                PermissionNodes.Admin.TICKETS_ISSUE,
                "no",
                Ct
            )
        )
            .Should()
            .Contain("can do things you can't");
    }

    [Fact]
    public async Task NotWithoutTheNode()
    {
        (await RefusalAsync(PLAYER, PLAYER)).Should().Contain("can't issue");
    }
}
