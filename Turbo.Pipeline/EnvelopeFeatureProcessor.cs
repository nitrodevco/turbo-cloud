using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Pipeline.Attributes;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Pipeline;

public class EnvelopeFeatureProcessor<TEnvelope, TMeta, TContext>(
    EnvelopeHost<TEnvelope, TMeta, TContext> registry,
    EnvelopeInvokerFactory<TContext> invokerFactory,
    Type openHandlerInterface,
    Type openBehaviorInterface
) : IAssemblyFeatureProcessor
{
    private readonly EnvelopeHost<TEnvelope, TMeta, TContext> _registry = registry;
    private readonly EnvelopeInvokerFactory<TContext> _invokerFactory = invokerFactory;
    private readonly Type _openHandlerInterface = openHandlerInterface;
    private readonly Type _openBehaviorInterface = openBehaviorInterface;

    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var batch = new CompositeDisposable();

        foreach (
            var (concrete, closedIface, args) in AssemblyExplorer.FindClosedImplementations(
                asm,
                _openHandlerInterface
            )
        )
        {
            var envType = args[0];
            var invoker = _invokerFactory.CreateHandlerInvoker(concrete, envType);
            var factory = ActivatorUtilities.CreateFactory(concrete, Type.EmptyTypes);
            object activator(IServiceProvider sp) => factory(sp, null);

            batch.Add(_registry.RegisterHandler(envType, sp, activator, invoker));
        }

        foreach (
            var (concrete, closedIface, args) in AssemblyExplorer.FindClosedImplementations(
                asm,
                _openBehaviorInterface
            )
        )
        {
            var envType = args[0];
            var invoker = _invokerFactory.CreateBehaviorInvoker(concrete, envType);
            var order = concrete.GetCustomAttribute<OrderAttribute>()?.Value ?? 0;
            var factory = ActivatorUtilities.CreateFactory(concrete, Type.EmptyTypes);

            object activator(IServiceProvider sp) => factory(sp, null);

            batch.Add(_registry.RegisterBehavior(envType, sp, activator, invoker, order));
        }

        return Task.FromResult<IDisposable>(batch);
    }
}
