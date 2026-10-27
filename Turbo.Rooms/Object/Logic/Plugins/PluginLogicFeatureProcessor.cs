using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Turbo.Contracts.Furni;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms.Object.Logic.Plugins;

/// <summary>
/// Finds a plugin's contract-only furni logic (<see cref="FurniLogic"/>) and wired boxes
/// (<see cref="WiredBox"/>) and registers each under its key, hosted by core's own kind
/// (<see cref="PluginFurniLogic"/>, <see cref="PluginWiredEffectLogic"/> and the rest), as
/// <see cref="RoomObjectLogicFeatureProcessor"/> registers logic written against the room's types.
/// </summary>
internal sealed class PluginLogicFeatureProcessor(IRoomObjectLogicProvider logics)
    : IAssemblyFeatureProcessor
{
    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var batch = new CompositeDisposable();

        foreach (var type in AssemblyExplorer.FindAssignees(asm, typeof(WiredBox)))
        {
            if (type?.GetCustomAttribute<WiredBoxAttribute>(false) is not { } attribute)
                continue;

            batch.Add(
                Register(sp, attribute.Key, type, $"wired {KindOf(type)} {type.Name}", Wired(type))
            );
        }

        foreach (var type in AssemblyExplorer.FindAssignees(asm, typeof(FurniLogic)))
        {
            if (type?.GetCustomAttribute<FurniLogicAttribute>(false) is not { } attribute)
                continue;

            // Sold, granted and refused as a gift in pairs, as a teleporter, while it is loaded.
            if (attribute.LinkedPair)
                batch.Add(TeleportFurniture.AddPairedLogic(attribute.Key));

            batch.Add(
                Register(
                    sp,
                    attribute.Key,
                    type,
                    attribute.LinkedPair ? $"{type.Name} (linked pair)" : type.Name,
                    (services, ctx) =>
                        new PluginFurniLogic(
                            services.GetRequiredService<IStuffDataFactory>(),
                            ctx,
                            (FurniLogic)ActivatorUtilities.CreateInstance(services, type),
                            attribute.LinkedPair
                        )
                )
            );
        }

        return Task.FromResult<IDisposable>(batch);
    }

    private IDisposable Register(
        IServiceProvider sp,
        string key,
        Type type,
        string detail,
        Func<IServiceProvider, IRoomFloorItemContext, IRoomObjectLogic> create
    )
    {
        // A key already registered (core's, or another plugin's) is replaced: an override.
        if (logics.LogicTypes is { } known && known.Contains(key))
            sp.Contributes(
                PluginContributionKinds.OVERRIDE,
                key,
                $"{type.Name} replaces the logic registered before"
            );

        sp.Contributes(
            typeof(WiredBox).IsAssignableFrom(type)
                ? PluginContributionKinds.WIRED_BOX
                : PluginContributionKinds.FURNI_LOGIC,
            key,
            detail
        );

        return logics.RegisterLogic(
            key,
            sp,
            (services, ctx) =>
                ctx is IRoomFloorItemContext floor
                    ? create(services, floor)
                    : throw new InvalidOperationException(
                        $"{type.Name} ({key}) is floor furni logic; a wall furni has that logic."
                    )
        );
    }

    private static Func<IServiceProvider, IRoomFloorItemContext, IRoomObjectLogic> Wired(
        Type type
    ) =>
        (services, ctx) =>
        {
            var grains = services.GetRequiredService<IGrainFactory>();
            var stuff = services.GetRequiredService<IStuffDataFactory>();

            return ActivatorUtilities.CreateInstance(services, type) switch
            {
                WiredEffect effect => new PluginWiredEffectLogic(grains, stuff, ctx, effect),
                WiredCondition condition => new PluginWiredConditionLogic(
                    grains,
                    stuff,
                    ctx,
                    condition
                ),
                WiredSelector selector => new PluginWiredSelectorLogic(
                    grains,
                    stuff,
                    ctx,
                    selector
                ),
                WiredTrigger trigger => new PluginWiredTriggerLogic(grains, stuff, ctx, trigger),
                var other => throw new InvalidOperationException(
                    $"{other.GetType().Name} is no wired kind the hotel runs."
                ),
            };
        };

    private static string KindOf(Type type) =>
        typeof(WiredEffect).IsAssignableFrom(type) ? "effect"
        : typeof(WiredCondition).IsAssignableFrom(type) ? "condition"
        : typeof(WiredSelector).IsAssignableFrom(type) ? "selector"
        : "trigger";
}
