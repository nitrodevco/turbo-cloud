using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Turbo.Messages.Registry;
using Turbo.Pipeline.Delegates;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Xunit;

namespace Turbo.Tests.Messages;

/// <summary>What <see cref="PermissionGate"/> lets through to a handler.</summary>
public class PermissionGateBehaviorTests
{
    private const int PLAYER = 7;

    [Fact]
    public void Ungated_KeepsTheInvokerAsItIs()
    {
        var invoker = Recording([]);

        PermissionGate.Wrap(typeof(UngatedHandler), Holds(), invoker).Should().BeSameAs(invoker);
    }

    [Fact]
    public async Task Gated_ReachesTheHandler_WhenThePlayerHoldsTheNode()
    {
        var reached = new List<int>();

        await InvokeAsync(
            typeof(ModeratorHandler),
            Holds(PermissionNodes.Moderation.TOOL),
            reached
        );

        reached.Should().Equal(PLAYER);
    }

    [Fact]
    public async Task Gated_SkipsTheHandler_WhenThePlayerLacksTheNode()
    {
        var reached = new List<int>();

        await InvokeAsync(
            typeof(ModeratorHandler),
            Holds(PermissionNodes.Room.MODERATE_ANY),
            reached
        );

        reached.Should().BeEmpty();
    }

    [Fact]
    public async Task Gated_AnyDeclaredNodeLetsThrough()
    {
        var reached = new List<int>();

        await InvokeAsync(
            typeof(AmbassadorHandler),
            Holds(PermissionNodes.Room.MODERATE_ANY),
            reached
        );

        reached.Should().Equal(PLAYER);
    }

    [Fact]
    public async Task Gated_SkipsTheHandler_WithoutASignedInPlayer_AndAsksNothing()
    {
        var reached = new List<int>();
        var asked = 0;

        await PermissionGate.Wrap(
            typeof(ModeratorHandler),
            (_, _, _) =>
            {
                asked++;
                return Task.FromResult(true);
            },
            Recording(reached)
        )(null!, null!, Context(0), CancellationToken.None);

        reached.Should().BeEmpty();
        asked.Should().Be(0);
    }

    private static Task InvokeAsync(
        Type handler,
        PermissionGate.PermissionCheck check,
        List<int> reached
    ) =>
        PermissionGate
            .Wrap(handler, check, Recording(reached))(
                null!,
                null!,
                Context(PLAYER),
                CancellationToken.None
            )
            .AsTask();

    private static PermissionGate.PermissionCheck Holds(params string[] nodes) =>
        (_, node, _) => Task.FromResult(Array.IndexOf(nodes, node) >= 0);

    private static HandlerInvoker<MessageContext> Recording(List<int> reached) =>
        (_, _, ctx, _) =>
        {
            reached.Add(ctx.PlayerId);
            return ValueTask.CompletedTask;
        };

    // The gate reads only the player id; the session is never touched.
    private static MessageContext Context(PlayerId playerId) => new(null!, playerId, -1);

    private sealed class UngatedHandler;

    [RequiresPermission(PermissionNodes.Moderation.TOOL)]
    private sealed class ModeratorHandler;

    [RequiresPermission(PermissionNodes.Role.AMBASSADOR, PermissionNodes.Room.MODERATE_ANY)]
    private sealed class AmbassadorHandler;
}
