using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Plugins;
using Turbo.Messages.Registry;
using Turbo.Pipeline.Delegates;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Messages.PluginMessages;

/// <summary>
/// Finds a plugin's named messages (<see cref="PluginMessageAttribute"/> records) and their
/// handlers (<see cref="IPluginMessageHandler{T}"/>) as it loads: the messages get their headers
/// (<see cref="IPluginMessageDirectory"/>), the handlers join the packet pipeline behind the same
/// gates as a packet handler. The handle it returns takes all of it away when the plugin unloads.
/// A message the directory refuses fails the plugin's load, naming why.
/// </summary>
internal sealed class PluginMessageFeatureProcessor(
    MessageRegistry registry,
    IPluginMessageDirectory directory,
    ILoggerFactory loggerFactory,
    IServiceProvider host
) : IAssemblyFeatureProcessor
{
    private static readonly MethodInfo CREATE_INVOKER =
        typeof(PluginMessageFeatureProcessor).GetMethod(
            nameof(CreateInvoker),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private readonly ILogger _signInLogger = loggerFactory.CreateLogger(typeof(SignInGate));

    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var messageTypes = AssemblyExplorer.FindAssignees(asm, typeof(IPluginMessage)).ToList();
        var handlers = AssemblyExplorer
            .FindClosedImplementations(asm, typeof(IPluginMessageHandler<>))
            .ToList();
        var batch = new CompositeDisposable();

        try
        {
            if (messageTypes.Count > 0)
            {
                var owner = sp.GetService<PluginManifest>()?.Key ?? HostOwner(messageTypes, asm);

                batch.Add(directory.Register(owner, messageTypes));

                foreach (var type in messageTypes)
                    sp.Contributes(
                        PluginContributionKinds.MESSAGE,
                        PluginMessageSchema.For(type).Name,
                        type.Name
                    );
            }

            foreach (var (concrete, _, args) in handlers)
            {
                var messageType = args[0];

                // Its schema refuses a type that is no message, before anything is registered.
                var schema = PluginMessageSchema.For(messageType);

                sp.Contributes(PluginContributionKinds.MESSAGE_HANDLER, concrete.Name, schema.Name);

                var invoker =
                    (HandlerInvoker<MessageContext>)
                        CREATE_INVOKER.MakeGenericMethod(messageType).Invoke(null, null)!;

                batch.Add(
                    registry.RegisterHandler(
                        typeof(PluginMessageEvent<>).MakeGenericType(messageType),
                        concrete,
                        sp,
                        ActivatorHelpers.BuildActivator(concrete),
                        MessageGates.Wrap(concrete, host, _signInLogger, invoker)
                    )
                );
            }
        }
        catch
        {
            // A refusal fails the plugin's load; what it did register must not outlive it.
            batch.Dispose();

            throw;
        }

        return Task.FromResult<IDisposable>(batch);
    }

    /// <summary>
    /// The owner of messages Turbo's own modules declare: <see cref="HOST_OWNER"/>, their names
    /// starting <c>turbo.</c>, so a plugin's never meets one. Any other name outside a plugin is refused.
    /// </summary>
    private static string HostOwner(
        System.Collections.Generic.List<Type> messageTypes,
        Assembly asm
    )
    {
        if (
            messageTypes.FirstOrDefault(x =>
                !PluginMessageSchema
                    .For(x)
                    .Name.StartsWith(HOST_OWNER + ".", StringComparison.OrdinalIgnoreCase)
            ) is
            { } stray
        )
            throw new InvalidOperationException(
                $"Only a plugin declares plugin messages ({stray.Name} in {asm.GetName().Name}); Turbo's own are named \"{HOST_OWNER}.\"."
            );

        return HOST_OWNER;
    }

    /// <summary>The owner, and name prefix, of the named messages Turbo's own modules declare.</summary>
    public const string HOST_OWNER = "turbo";

    private static HandlerInvoker<MessageContext> CreateInvoker<T>()
        where T : IPluginMessage =>
        (inst, env, ctx, ct) =>
            ((IPluginMessageHandler<T>)inst).HandleAsync(
                ((PluginMessageEvent<T>)env).Message,
                new HostPluginMessageContext(ctx),
                ct
            );
}
