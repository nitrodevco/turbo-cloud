using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Plugins;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Authentication;

/// <summary>
/// Registers the <see cref="IPlayPolicy"/>s an assembly declares, so a plugin's policies run while
/// it is loaded and go when it unloads. Each is built once with the plugin's services and must be
/// public to be found.
/// </summary>
internal sealed class PlayPolicyFeatureProcessor(PlayPolicies policies) : IAssemblyFeatureProcessor
{
    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var found = AssemblyExplorer
            .FindAssignees(asm, typeof(IPlayPolicy))
            .Select(concrete => (IPlayPolicy)ActivatorUtilities.CreateInstance(sp, concrete))
            .ToArray();

        if (found.Length == 0)
            return Task.FromResult<IDisposable>(new CompositeDisposable());

        var owner = sp.GetService<PluginManifest>()?.Key ?? asm.GetName().Name ?? "core";

        foreach (var policy in found)
            sp.Contributes(PluginContributionKinds.PLAY_POLICY, policy.GetType().Name);

        return Task.FromResult(policies.Register(owner, found));
    }
}
