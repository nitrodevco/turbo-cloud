using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms.Object.Logic;

internal class RoomObjectLogicFeatureProcessor(IRoomObjectLogicProvider roomObjectLogicFactory)
    : IAssemblyFeatureProcessor
{
    private readonly IRoomObjectLogicProvider _roomObjectLogicFactory = roomObjectLogicFactory;

    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var batch = new CompositeDisposable();

        foreach (var concrete in AssemblyExplorer.FindAssignees(asm, typeof(IRoomObjectLogic)))
        {
            if (concrete is null)
                continue;

            var attribute = concrete.GetCustomAttribute<RoomObjectLogicAttribute>(false);

            if (attribute is null)
                continue;

            // A key already registered (core's, or another plugin's) is replaced: an override.
            if (_roomObjectLogicFactory.LogicTypes.Contains(attribute.Key))
                sp.Contributes(
                    PluginContributionKinds.OVERRIDE,
                    attribute.Key,
                    $"furni logic {concrete.Name} replaces the one registered before"
                );

            sp.Contributes(PluginContributionKinds.FURNI_LOGIC, attribute.Key, concrete.Name);

            batch.Add(
                _roomObjectLogicFactory.RegisterLogic(
                    attribute.Key,
                    sp,
                    (sp, ctx) =>
                        (IRoomObjectLogic)ActivatorUtilities.CreateInstance(sp, concrete, ctx)
                )
            );
        }

        return Task.FromResult<IDisposable>(batch);
    }
}
