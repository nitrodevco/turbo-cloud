using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
using Turbo.Primitives.Furniture.WiredData;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureWiredLogic : IFurnitureFloorLogic
{
    public WiredType WiredType { get; }
    public int WiredCode { get; }
    public IWiredData WiredData { get; }
    public Task RefreshWiredParamsAsync(CancellationToken ct);
    public Task LoadWiredAsync(CancellationToken ct);
    public Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWired update,
        CancellationToken ct
    );
    public WiredDataSnapshot GetSnapshot();
}
