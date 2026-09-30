using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

public class PlayerPermissionsChangedEventTests
{
    [Fact]
    public void HoldsSame_IgnoresOrderAndInstances()
    {
        var a = Resolved(["trade", "chat.speak"], ("limit.rooms", "50"));
        var b = Resolved(["chat.speak", "trade"], ("limit.rooms", "50"));

        a.HoldsSame(b).Should().BeTrue();
    }

    [Fact]
    public void HoldsSame_SeesANodeOrMetaChange()
    {
        var before = Resolved(["trade"], ("limit.rooms", "50"));

        before
            .HoldsSame(Resolved(["trade", "chat.speak"], ("limit.rooms", "50")))
            .Should()
            .BeFalse();
        before.HoldsSame(Resolved(["trade"], ("limit.rooms", "100"))).Should().BeFalse();
        before.HoldsSame(Resolved(["trade"])).Should().BeFalse();
    }

    [Fact]
    public void GainedAndLost_AreTheDifference()
    {
        var evt = new PlayerPermissionsChangedEvent
        {
            PlayerId = PlayerId.Parse(1),
            Previous = Resolved(["trade", "chat.speak"]),
            Current = Resolved(["chat.speak", "room.enter.locked"]),
        };

        evt.Gained.Should().Equal("room.enter.locked");
        evt.Lost.Should().Equal("trade");
    }

    private static ResolvedPermissionsSnapshot Resolved(
        string[] granted,
        params (string Key, string Value)[] meta
    ) =>
        ResolvedPermissionsSnapshot.EMPTY with
        {
            Granted = [.. granted],
            Meta = meta.ToImmutableDictionary(x => x.Key, x => x.Value),
        };
}
