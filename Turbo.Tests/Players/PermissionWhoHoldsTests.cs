using System.Collections.Concurrent;
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
    [Fact]
    public async Task The_players_are_asked_at_once_and_only_holders_come_back()
    {
        const int PLAYERS = 20;
        var fakes = new Fakes();
        var pending = new ConcurrentDictionary<long, TaskCompletionSource<bool>>();
        fakes.Handlers["HasAsync"] = call =>
            pending
                .GetOrAdd(
                    Convert.ToInt64(call.Key),
                    _ => new(TaskCreationOptions.RunContinuationsAsynchronously)
                )
                .Task;

        var asking = fakes
            .Create<IGrainFactory>()
            .WhoHoldsAsync(
                Enumerable.Range(1, PLAYERS).Select(x => new PlayerId(x)),
                "moderation.tool",
                TestContext.Current.CancellationToken
            );

        // No grain has answered yet: asked one after another, only the first would be waiting.
        asking.IsCompleted.Should().BeFalse();
        pending.Keys.Order().Should().Equal(Enumerable.Range(1, PLAYERS).Select(x => (long)x));

        foreach (var (playerId, answer) in pending)
        {
            answer.SetResult(playerId % 2 == 0);
        }

        var holders = await asking;

        holders.Select(x => x.Value).Should().Equal(2, 4, 6, 8, 10, 12, 14, 16, 18, 20);
    }
}
