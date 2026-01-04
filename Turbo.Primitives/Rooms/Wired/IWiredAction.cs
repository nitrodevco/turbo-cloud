using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredAction : IWiredItem
{
    public int GetDelayMs();
    public Task<bool> ExecuteAsync(WiredContextSnapshot ctx, CancellationToken ct);
}
