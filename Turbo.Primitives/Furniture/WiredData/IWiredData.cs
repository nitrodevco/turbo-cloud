using System;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots.WiredData;

namespace Turbo.Primitives.Furniture.WiredData;

public interface IWiredData
{
    public int WiredCode { get; set; }
    public int FurniLimit { get; set; }
    public void SetAction(Func<Task>? onSnapshotChanged);
    public WiredDataSnapshot GetSnapshot();
}
