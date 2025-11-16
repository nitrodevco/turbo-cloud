using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms.Furniture.Logic;

public class FurnitureLogicFeatureProcessor(IFurnitureLogicFactory furnitureLogicFactory)
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

            var activator = ActivatorHelpers.BuildActivator(concrete);
            var logicType = attribute.Key;

            batch.Add(_furnitureLogicFactory.RegisterLogic(logicType, sp, activator));
        }

        return Task.FromResult<IDisposable>(batch);
    }
}
