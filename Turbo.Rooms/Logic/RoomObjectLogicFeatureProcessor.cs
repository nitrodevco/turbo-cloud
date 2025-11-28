using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms.Logic;

internal class RoomObjectLogicFeatureProcessor(IRoomObjectLogicFactory roomObjectLogicFactory)
    : IAssemblyFeatureProcessor
{
    private readonly IRoomObjectLogicFactory _roomObjectLogicFactory = roomObjectLogicFactory;

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
