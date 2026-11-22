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

        foreach (var (concrete, key) in RoomObjectLogics.In(asm))
        {
            // A key already registered (core's, or another plugin's) is replaced: an override.
            if (_roomObjectLogicFactory.LogicTypes is { } known && known.Contains(key))
                sp.Contributes(
                    PluginContributionKinds.OVERRIDE,
                    key,
                    $"furni logic {concrete.Name} replaces the one registered before"
                );

            sp.Contributes(PluginContributionKinds.FURNI_LOGIC, key, concrete.Name);

            batch.Add(
                _roomObjectLogicFactory.RegisterLogic(key, sp, (sp, ctx) =>
                    (IRoomObjectLogic)ActivatorUtilities.CreateInstance(sp, concrete, ctx)
                )
            );
        }

        return Task.FromResult<IDisposable>(batch);
    }
}
