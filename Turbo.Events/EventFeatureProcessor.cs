using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Events.Registry;
using Turbo.Pipeline.Attributes;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Events;

internal sealed class EventFeatureProcessor(
    EventRegistry registry,
    EventInvokerFactory invokerFactory
) : IAssemblyFeatureProcessor
{
    private readonly EventRegistry _registry = registry;
    private readonly EventInvokerFactory _invokerFactory = invokerFactory;

    public Task<IDisposable> ProcessAsync(
        Assembly assembly,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var batch = new CompositeDisposable();

        foreach (
            var (concrete, closedIface, args) in AssemblyExplorer.FindClosedImplementations(
                assembly,
                typeof(IEventHandler<>)
            )
        )
        {
            var envType = args[0];

            try
            {
                var invoker = _invokerFactory.CreateHandlerInvoker(concrete, envType);
                var factory = ActivatorUtilities.CreateFactory(concrete, Type.EmptyTypes);

                object activator(IServiceProvider sp) => factory(sp, null);

                batch.Add(_registry.RegisterHandler(envType, sp, activator, invoker));
            }
            catch (Exception ex) { }
        }

        foreach (
            var (concrete, closedIface, args) in AssemblyExplorer.FindClosedImplementations(
                assembly,
                typeof(IEventBehavior<>)
            )
        )
        {
            var envType = args[0];

            try
            {
                var invoker = _invokerFactory.CreateBehaviorInvoker(concrete, envType);
                var order = concrete.GetCustomAttribute<OrderAttribute>()?.Value ?? 0;
                var factory = ActivatorUtilities.CreateFactory(concrete, Type.EmptyTypes);

                object activator(IServiceProvider sp) => factory(sp, null);

                batch.Add(_registry.RegisterBehavior(envType, sp, activator, invoker, order));
            }
            catch (Exception ex) { }
        }

        return Task.FromResult<IDisposable>(batch);
    }
}
