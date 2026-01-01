using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredAction : IWiredItem
{
    public int GetDelayMs();
    public Task<bool> ExecuteAsync(IWiredContext ctx, CancellationToken ct);
}
