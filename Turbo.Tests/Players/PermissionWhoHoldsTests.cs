using System.Diagnostics;
using FluentAssertions;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// Which online players hold a node (the moderators a call for help goes to, who stays through a
/// maintenance): their permission grains are asked at once. Asked one after another, as both
/// did, every online player added a grain call's time.
/// </summary>
public sealed class PermissionWhoHoldsTests
{
    private const int CALL_MS = 100;

    [Fact]
    public async Task The_players_are_asked_at_once_and_only_holders_come_back()
    {
        const int PLAYERS = 20;
        var fakes = new Fakes();
        fakes.Handlers["HasAsync"] = call => HeldAfterAWhileAsync(Convert.ToInt64(call.Key));
        var clock = Stopwatch.StartNew();

        var holders = await fakes
            .Create<IGrainFactory>()
            .WhoHoldsAsync(
                Enumerable.Range(1, PLAYERS).Select(x => new PlayerId(x)),
                "moderation.tool",
                TestContext.Current.CancellationToken
            );

        holders.Select(x => x.Value).Should().Equal(2, 4, 6, 8, 10, 12, 14, 16, 18, 20);
        clock
            .ElapsedMilliseconds.Should()
            .BeLessThan(
                PLAYERS * CALL_MS / 2,
                "one after another it takes {0} ms",
                PLAYERS * CALL_MS
            );
    }

    private static async Task<bool> HeldAfterAWhileAsync(long playerId)
    {
        await Task.Delay(CALL_MS);

        return playerId % 2 == 0;
    }
}
