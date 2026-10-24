using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Players.Grains;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// A packet handler sends a composer one of the three ways CLAUDE.md names: to its own session
/// (<c>ctx.SendComposerAsync</c>), to a player (<c>SendComposerToPlayerAsync</c>) or to a room.
/// It does not spell out <c>GetPlayerPresenceGrain(id).SendComposerAsync(...)</c>, which
/// <c>TurboClientCapabilitiesMessageHandler</c> did; this reads every handler's compiled
/// <c>HandleAsync</c> so it does not come back.
/// </summary>
public class HandlerSendTests
{
    private static readonly MethodInfo[] PRESENCE_SENDS =
    [
        .. typeof(IPlayerPresenceGrain)
            .GetMethods()
            .Where(x => x.Name == nameof(IPlayerPresenceGrain.SendComposerAsync)),
    ];

    [Fact]
    public void Every_handler_sends_only_the_sanctioned_ways()
    {
        var offenders = PermissionGateTests
            .HandlerTypes()
            .Where(handler =>
                PermissionGateTests.ReadHandleAsyncBody(handler).Calls.Overlaps(PRESENCE_SENDS)
            )
            .Select(handler =>
                $"{handler.Name} sends through the presence grain itself; use SendComposerToPlayerAsync"
            );

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void The_presence_sends_are_found() => PRESENCE_SENDS.Should().HaveCount(2);
}
