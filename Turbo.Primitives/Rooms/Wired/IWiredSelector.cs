using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredSelector : IWiredItem
{
    public Task<IWiredSelectionSet> SelectAsync(IWiredContext ctx, CancellationToken ct);
}
