using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms.Furniture.Logic;

internal class FurnitureLogicFeatureProcessor(IFurnitureLogicFactory furnitureLogicFactory)
    : IAssemblyFeatureProcessor
{
    private readonly IFurnitureLogicFactory _furnitureLogicFactory = furnitureLogicFactory;

    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var batch = new CompositeDisposable();

        foreach (var concrete in AssemblyExplorer.FindAssignees(asm, typeof(IFurnitureLogic)))
        {
            if (concrete is null)
                continue;

            var attribute = concrete.GetCustomAttribute<FurnitureLogicAttribute>(false);

            if (attribute is null)
                continue;

            batch.Add(
                _furnitureLogicFactory.RegisterLogic(
                    attribute.Key,
                    sp,
                    (sp, ctx) =>
                        (IFurnitureLogic)ActivatorUtilities.CreateInstance(sp, concrete, ctx)
                )
            );
        }

        return Task.FromResult<IDisposable>(batch);
    }
}
