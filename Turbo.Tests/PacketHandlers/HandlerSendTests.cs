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

    public static TheoryData<Type> Handlers()
    {
        var data = new TheoryData<Type>();

        foreach (var type in PermissionGateTests.HandlerTypes())
            data.Add(type);

        return data;
    }

    [Theory]
    [MemberData(nameof(Handlers))]
    public void Handler_SendsOnlyTheSanctionedWays(Type handler)
    {
        var (_, calls) = PermissionGateTests.ReadHandleAsyncBody(handler);

        calls
            .Intersect(PRESENCE_SENDS)
            .Should()
            .BeEmpty(
                "{0} sends through the presence grain itself; use SendComposerToPlayerAsync",
                handler.Name
            );
    }

    [Fact]
    public void The_presence_sends_are_found() => PRESENCE_SENDS.Should().HaveCount(2);
}
