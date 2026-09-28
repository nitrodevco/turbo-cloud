using FluentAssertions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

public class ResolvedPermissionsSnapshotTests
{
    [Fact]
    public void IsWatched_OffByDefault()
    {
        ResolvedPermissionsSnapshot.EMPTY.IsWatched("trade").Should().BeFalse();
    }

    [Fact]
    public void IsWatched_EmptyFilterWatchesEverything()
    {
        var resolved = ResolvedPermissionsSnapshot.EMPTY with { VerboseFilter = "" };

        resolved.IsWatched("trade").Should().BeTrue();
        resolved.IsWatched("limit.rooms").Should().BeTrue();
    }

    [Fact]
    public void IsWatched_FilterIsAPrefix()
    {
        var resolved = ResolvedPermissionsSnapshot.EMPTY with { VerboseFilter = "room." };

        resolved.IsWatched("room.enter.locked").Should().BeTrue();
        resolved.IsWatched("trade").Should().BeFalse();
    }

    [Fact]
    public void HoldsSame_IgnoresVerbose()
    {
        var watched = ResolvedPermissionsSnapshot.EMPTY with { VerboseFilter = "" };

        watched.HoldsSame(ResolvedPermissionsSnapshot.EMPTY).Should().BeTrue();
    }
}
