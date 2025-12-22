using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms.Wired;

internal class WiredFeatureProcessor(IWiredDefinitionProvider wiredDefinitionProvider)
    : IAssemblyFeatureProcessor
{
    private readonly IWiredDefinitionProvider _wiredDefinitionProvider = wiredDefinitionProvider;

    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var batch = new CompositeDisposable();

        foreach (var concrete in AssemblyExplorer.FindAssignees(asm, typeof(IWiredDefinition)))
        {
            if (concrete is null)
                continue;

            var attribute = concrete.GetCustomAttribute<WiredDefinitionAttribute>(false);

            if (attribute is null)
                continue;

            batch.Add(
                _wiredDefinitionProvider.RegisterDefinition(
                    attribute.Key,
                    sp,
                    (sp, ctx) =>
                        (IWiredDefinition)ActivatorUtilities.CreateInstance(sp, concrete, ctx)
                )
            );
        }

        return Task.FromResult<IDisposable>(batch);
    }
}
