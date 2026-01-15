using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms.Wired.Variables;

internal class WiredVariableFeatureProcessor(IRoomWiredVariablesProvider wiredVariablesProvider)
    : IAssemblyFeatureProcessor
{
    private readonly IRoomWiredVariablesProvider _wiredVariablesProvider = wiredVariablesProvider;

    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var batch = new CompositeDisposable();

        foreach (var concrete in AssemblyExplorer.FindAssignees(asm, typeof(IWiredVariable)))
        {
            if (concrete is null)
                continue;

            var attribute = concrete.GetCustomAttribute<WiredVariableAttribute>(false);

            if (attribute is null)
                continue;

            batch.Add(
                _wiredVariablesProvider.RegisterVariable(
                    attribute.Key,
                    sp,
                    (sp, ctx) =>
                        (IWiredVariable)ActivatorUtilities.CreateInstance(sp, concrete, ctx)
                )
            );
        }

        return Task.FromResult<IDisposable>(batch);
    }
}
