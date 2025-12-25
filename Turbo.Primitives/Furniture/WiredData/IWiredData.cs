using System;
using System.Threading.Tasks;

namespace Turbo.Primitives.Furniture.WiredData;

public interface IWiredData
{
    public void SetAction(Func<Task>? onSnapshotChanged);
}
