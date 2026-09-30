using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Players.Permissions;

/// <summary>
/// Registers the <see cref="IPermissionNodeSource"/>s an assembly declares, so a plugin's nodes
/// exist while it is loaded and go when it unloads. Core's source is not discovered here: the
/// provider always holds it. A source is built with the plugin's services, and must be public
/// to be found.
/// </summary>
internal sealed class PermissionNodeFeatureProcessor(
    IPermissionRegistryProvider permissionRegistryProvider
) : IAssemblyFeatureProcessor
{
    private readonly IPermissionRegistryProvider _permissionRegistryProvider =
        permissionRegistryProvider;

    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var batch = new CompositeDisposable();

        try
        {
            foreach (
                var concrete in AssemblyExplorer.FindAssignees(asm, typeof(IPermissionNodeSource))
            )
            {
                if (concrete == typeof(CorePermissionNodeSource))
                    continue;

                var source = (IPermissionNodeSource)ActivatorUtilities.CreateInstance(sp, concrete);

                batch.Add(_permissionRegistryProvider.Register(source));
            }
        }
        catch
        {
            // A clash fails the plugin's load; what it did register must not outlive it.
            batch.Dispose();

            throw;
        }

        return Task.FromResult<IDisposable>(batch);
    }
}
