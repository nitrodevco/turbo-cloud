using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Messages.Registry;
using Turbo.Pipeline.Attributes;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Messages;

internal sealed class MessageFeatureProcessor(
    MessageRegistry registry,
    MessageInvokerFactory invokerFactory
) : IAssemblyFeatureProcessor
{
    private readonly MessageRegistry _registry = registry;
    private readonly MessageInvokerFactory _invokerFactory = invokerFactory;

    public IDisposable Process(Assembly assembly, IServiceProvider sp)
    {
        var batch = new CompositeDisposable();

        foreach (
            var (concrete, closedIface, args) in AssemblyExplorer.FindClosedImplementations(
                assembly,
                typeof(IMessageHandler<>)
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
                assembly,
                typeof(IMessageBehavior<>)
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

        return batch;
    }
}
