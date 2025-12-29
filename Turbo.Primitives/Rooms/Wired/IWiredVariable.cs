using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredVariable : IWiredItem
{
    public Task ApplyAsync(IWiredContext ctx, CancellationToken ct);
}
