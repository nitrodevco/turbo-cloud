using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.Policies;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Players.Policies;

/// <summary>
/// Registers the game policies an assembly declares (<see cref="IPurchasePolicy"/>,
/// <see cref="IRoomEntryPolicy"/>, <see cref="IPlacementPolicy"/>, <see cref="IRegistrationPolicy"/>, <see cref="IAvatarEffectPolicy"/>,
/// <see cref="IPurchaseFulfilment"/>, <see cref="IPublicRoomOrder"/>, <see cref="IRoomArrivalPolicy"/>), so a plugin's policies run
/// while it is loaded and go when it unloads. Each class is built once with the plugin's services
/// (one implementing several kinds is one instance) and must be public to be found.
/// </summary>
internal sealed class GamePolicyFeatureProcessor(GamePolicies policies) : IAssemblyFeatureProcessor
{
    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var found = new[]
        {
            typeof(IPurchasePolicy),
            typeof(IRoomEntryPolicy),
            typeof(IPlacementPolicy),
            typeof(IRegistrationPolicy),
            typeof(IAvatarEffectPolicy),
            typeof(IPurchaseFulfilment),
            typeof(IPublicRoomOrder),
            typeof(IRoomArrivalPolicy),
        }
            .SelectMany(kind => AssemblyExplorer.FindAssignees(asm, kind))
            .Distinct()
            .Select(concrete => ActivatorUtilities.CreateInstance(sp, concrete))
            .ToArray();

        if (found.Length == 0)
            return Task.FromResult<IDisposable>(new CompositeDisposable());

        var owner = sp.GetService<PluginManifest>()?.Key ?? asm.GetName().Name ?? "core";

        return Task.FromResult(policies.Register(owner, found));
    }
}
