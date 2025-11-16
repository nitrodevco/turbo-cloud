using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Contracts.Enums.Rooms.Furniture.Data;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.StuffData;

namespace Turbo.Rooms.Furniture.Logic;

public abstract class FurnitureLogicBase : IFurnitureLogic
{
    public virtual StuffDataTypeEnum StuffDataKey => StuffDataTypeEnum.LegacyKey;

    public virtual ValueTask OnInteractAsync(FurnitureContext ctx, CancellationToken ct) =>
        ValueTask.CompletedTask;

    public virtual ValueTask OnMoveAsync(FurnitureContext ctx, CancellationToken ct) =>
        ValueTask.CompletedTask;

    public virtual ValueTask OnStopAsync(FurnitureContext ctx, CancellationToken ct) =>
        ValueTask.CompletedTask;

    public virtual ValueTask OnPlaceAsync(FurnitureContext ctx, CancellationToken ct) =>
        ValueTask.CompletedTask;

    public virtual ValueTask OnPickupAsync(FurnitureContext ctx, CancellationToken ct) =>
        ValueTask.CompletedTask;

    public virtual bool CanToggle(FurnitureContext ctx) => false;

    public virtual FurniUsagePolicy GetUsagePolicy(FurnitureContext ctx) =>
        ctx.DefinitionSnapshot.UsagePolicy;

    public virtual Task SetStateAsync(FurnitureContext ctx, string state, CancellationToken ct) =>
        Task.CompletedTask;

    public virtual IStuffData CreateStuffData() =>
        StuffDataFactory.CreateStuffData((int)StuffDataKey);

    public virtual IStuffData CreateStuffDataFromJson(string json) =>
        StuffDataFactory.CreateStuffDataFromJson((int)StuffDataKey, json);
}
