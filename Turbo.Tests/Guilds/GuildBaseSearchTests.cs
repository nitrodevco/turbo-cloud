using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Guilds.Grains;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Guilds;

/// <summary>
/// The navigator's <c>groups</c> search (the group window's "show groups"): Habbo's returned 60
/// homerooms (official client capture 2026-10-13).
/// </summary>
public sealed class GuildBaseSearchTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task the_groups_search_returns_habbos_60_biggest_groups()
    {
        var grain = GrainHarness.Create<GuildDirectoryGrain>(new Fakes());
        var state = GrainHarness.Get<GuildDirectoryLiveState>(grain);
        var summaries = state.SummaryByGuildId;
        var members = state.MemberCountByGuildId;

        for (var id = 1; id <= 70; id++)
        {
            summaries[id] = (
                (GuildSummarySnapshot)
                    RuntimeHelpers.GetUninitializedObject(typeof(GuildSummarySnapshot))
            ) with
            {
                GuildId = id,
                RoomId = 1000 + id,
            };
            members[id] = id;
        }

        var rooms = await ((IGuildDirectoryGrain)grain).GetGuildBaseRoomIdsAsync(Ct);

        rooms.Should().HaveCount(60);
        rooms[0].Value.Should().Be(1070);
        rooms[^1].Value.Should().Be(1011);
    }
}
