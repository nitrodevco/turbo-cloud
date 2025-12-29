using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureWiredLogic : IFurnitureFloorLogic, IWiredItem
{
    public WiredType WiredType { get; }
    public int WiredCode { get; }
    public Task LoadWiredAsync(CancellationToken ct);
    public Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    );
    public WiredDataSnapshot GetSnapshot();
}
