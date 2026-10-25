using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Events;
using Turbo.Events.Registry;
using Turbo.Pipeline;
using Turbo.Primitives.Events;

namespace Turbo.Tests.Support;

/// <summary>
/// The real global event pipeline with handlers a test writes as lambdas, so a test can watch an
/// event go by, or cancel it, the way a plugin's handler does.
/// </summary>
public sealed class TestEventBus
{
    private readonly ServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .BuildServiceProvider();
    private readonly EventRegistry _registry;

    public TestEventBus()
    {
        _registry = new EventRegistry(
            _services,
            _services.GetRequiredService<ILogger<EventRegistry>>()
        );
        System = new EventSystem(_registry);
    }

    public EventSystem System { get; }

    /// <summary>
    /// The failure guard of <see cref="WhenPublishedAsync{T}"/>: a passing test never waits it out,
    /// so it is long enough for a thread pool starved by the parallel suite (CI) to catch up.
    /// </summary>
    public static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    // A post-commit event reaches its handlers on the thread pool, so records come from several
    // threads at once (a plain list lost 444 of 2000, TestEventBusTests).
    private readonly Lock _gate = new();
    private readonly List<IEvent> _published = [];
    private TaskCompletionSource _recorded = NewSignal();

    /// <summary>What was recorded so far, in the order it was, as a copy.</summary>
    public IReadOnlyList<IEvent> Published
    {
        get
        {
            lock (_gate)
                return [.. _published];
        }
    }

    public IEnumerable<T> Of<T>()
        where T : IEvent => Published.OfType<T>();

    /// <summary>
    /// Waits until <paramref name="count"/> events of the type were recorded, woken by each record
    /// and failing after <see cref="Bound"/> if they never come, then until no more came for
    /// <see cref="CountWait.Quiet"/> (a publish is usually told, not awaited, and nothing says when
    /// every told one has run), so a test may assert exactly that many, none included.
    /// </summary>
    public async Task WhenPublishedAsync<T>(int count)
        where T : IEvent
    {
        // Lets what is already queued run first (under SessionHarness.InTurns, every turn queued).
        await Task.Yield();

        using var bound = new CancellationTokenSource(Bound);

        while (true)
        {
            // Taken before counting, so a record between the count and the wait still wakes it.
            var recorded = NextRecord();
            var seen = Of<T>().Count();

            if (seen >= count)
                break;

            try
            {
                await recorded.WaitAsync(bound.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"{count} {typeof(T).Name} expected within {Bound.TotalSeconds} s, {Of<T>().Count()} recorded."
                );
            }
        }

        while (!bound.IsCancellationRequested)
        {
            var recorded = NextRecord();

            if (await Task.WhenAny(recorded, Task.Delay(CountWait.Quiet)) != recorded)
                return;
        }
    }

    public void On<T>(Action<T> handler)
        where T : class, IEvent
    {
        var instance = new LambdaHandler<T>(handler);

        _registry.RegisterHandler(
            typeof(T),
            typeof(LambdaHandler<T>),
            _services,
            _ => instance,
            new EnvelopeInvokerFactory<EventContext>().CreateHandlerInvoker(
                typeof(LambdaHandler<T>),
                typeof(T)
            )
        );
    }

    /// <summary>Records every event of the types given, so <see cref="Published"/> lists them.</summary>
    public void Record<T>()
        where T : class, IEvent => On<T>(Add);

    private void Add(IEvent evt)
    {
        TaskCompletionSource recorded;

        lock (_gate)
        {
            _published.Add(evt);
            recorded = _recorded;
            _recorded = NewSignal();
        }

        recorded.SetResult();
    }

    private Task NextRecord()
    {
        lock (_gate)
            return _recorded.Task;
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void OnAsync<T>(Func<T, CancellationToken, ValueTask> handler)
        where T : class, IEvent
    {
        var instance = new AsyncLambdaHandler<T>(handler);
        _registry.RegisterHandler(
            typeof(T),
            typeof(AsyncLambdaHandler<T>),
            _services,
            _ => instance,
            new EnvelopeInvokerFactory<EventContext>().CreateHandlerInvoker(
                typeof(AsyncLambdaHandler<T>),
                typeof(T)
            )
        );
    }

    public sealed class AsyncLambdaHandler<T>(Func<T, CancellationToken, ValueTask> handler)
        : IEventHandler<T>
        where T : class, IEvent
    {
        public ValueTask HandleAsync(T env, EventContext ctx, CancellationToken ct) =>
            handler(env, ct);
    }

    public sealed class LambdaHandler<T>(Action<T> handler) : IEventHandler<T>
        where T : class, IEvent
    {
        public ValueTask HandleAsync(T env, EventContext ctx, CancellationToken ct)
        {
            handler(env);

            return ValueTask.CompletedTask;
        }
    }
}
