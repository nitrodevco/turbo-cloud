using FluentAssertions;
using Turbo.Primitives.Events;
using Xunit;

namespace Turbo.Tests.Support;

/// <summary>
/// The recording bus the event tests read: a post-commit event reaches its handlers on the thread
/// pool, so two of them can be recorded at the same moment.
/// </summary>
public sealed class TestEventBusTests
{
    private sealed record Told(int N) : DomainEvent;

    [Fact]
    public async Task Events_told_from_many_threads_at_once_are_all_recorded()
    {
        // PlayerAccountServiceTests failed on CI ("Sequence contains no elements", 2 of 7 main runs):
        // the bus added to a plain list from two pool threads and one of the two events was lost.
        const int TOLD = 2000;
        var events = new TestEventBus();

        events.Record<Told>();
        Parallel.For(0, TOLD, n => events.System.PublishCommitted(new Told(n)));

        await events.WhenPublishedAsync<Told>(TOLD);

        events.Of<Told>().Select(x => x.N).Should().BeEquivalentTo(Enumerable.Range(0, TOLD));
    }
}
