using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Orleans;
using Turbo.Ai;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Ai;

/// <summary>
/// Runs a minute (<c>docs/ai-sdk.md</c>, Limits) are counted for the whole cluster: one grain per
/// player and per plugin, so a player's runs spread over two servers count together, and a run one
/// key refuses takes back what the keys before it counted.
/// </summary>
public sealed class AiRateLimitTests
{
    [Fact]
    public async Task A_failed_later_grain_releases_the_earlier_reservation()
    {
        var fakes = new Fakes();
        var first = GrainHarness.Create<AiRateLimitGrain>(fakes);
        GrainHarness.Replace<TimeProvider>(first, new ManualTimeProvider());
        var failures = new Fakes();
        failures.Handlers["TryStartAsync"] = _ =>
            Task.FromException<bool>(new InvalidOperationException("RPC failed"));
        var failed = failures.Create<IAiRateLimitGrain>();
        fakes.Handlers["GetGrain"] = call => (string)call.Args[0]! == "first" ? first : failed;
        var limits = new ClusterAiRateLimits(fakes.Create<IGrainFactory>());
        var attempt = () =>
            limits.TryStartAsync(CancellationToken.None, ("first", 1), ("failed", 1));
        await attempt.Should().ThrowAsync<InvalidOperationException>();
        (await limits.TryStartAsync(CancellationToken.None, ("first", 1))).Should().BeTrue();
    }

    [Fact]
    public async Task Rolling_back_an_older_run_preserves_a_newer_runs_expiry()
    {
        var clock = new ManualTimeProvider();
        var grain = GrainHarness.Create<AiRateLimitGrain>(new Fakes());
        GrainHarness.Replace<TimeProvider>(grain, clock);
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        (await grain.TryStartAsync(older, 2, CancellationToken.None)).Should().BeTrue();
        clock.Advance(TimeSpan.FromSeconds(30));
        (await grain.TryStartAsync(newer, 2, CancellationToken.None)).Should().BeTrue();
        await grain.UndoAsync(older, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(30));
        (await grain.TryStartAsync(Guid.NewGuid(), 1, CancellationToken.None)).Should().BeFalse();
        clock.Advance(TimeSpan.FromSeconds(30));
        (await grain.TryStartAsync(Guid.NewGuid(), 1, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Two_servers_count_a_players_runs_together_and_a_refused_run_counts_nothing()
    {
        var clock = new ManualTimeProvider();
        var fakes = new Fakes();
        var activations = new ConcurrentDictionary<string, AiRateLimitGrain>();
        fakes.Handlers["GetGrain"] = call =>
            activations.GetOrAdd(
                (string)call.Args[0]!,
                _ =>
                {
                    var grain = GrainHarness.Create<AiRateLimitGrain>(fakes);
                    GrainHarness.Replace<TimeProvider>(grain, clock);

                    return grain;
                }
            );
        var one = new ClusterAiRateLimits(fakes.Create<IGrainFactory>());
        var two = new ClusterAiRateLimits(fakes.Create<IGrainFactory>());

        (await one.TryStartAsync(CancellationToken.None, ("plugin:a", 10), ("player:7", 2)))
            .Should()
            .BeTrue();
        (await two.TryStartAsync(CancellationToken.None, ("plugin:b", 10), ("player:7", 2)))
            .Should()
            .BeTrue();
        (await one.TryStartAsync(CancellationToken.None, ("plugin:a", 10), ("player:7", 2)))
            .Should()
            .BeFalse("the player started two runs this minute, one on each server");

        // Plugin c allows one run: the player's run it refuses is not counted against them.
        clock.Advance(TimeSpan.FromMinutes(1));
        (await two.TryStartAsync(CancellationToken.None, ("player:8", 1), ("plugin:c", 1)))
            .Should()
            .BeTrue();
        (await one.TryStartAsync(CancellationToken.None, ("player:9", 1), ("plugin:c", 1)))
            .Should()
            .BeFalse();
        (await one.TryStartAsync(CancellationToken.None, ("player:9", 1), ("plugin:a", 10)))
            .Should()
            .BeTrue();
    }
}
