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

    public List<IEvent> Published { get; } = [];

    public IEnumerable<T> Of<T>()
        where T : IEvent => Published.OfType<T>();

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
        where T : class, IEvent => On<T>(Published.Add);

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
