using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredSelector : IWiredItem
{
    public bool GetIsFilter();
    public bool GetIsInvert();
    public Task<IWiredSelectionSet> SelectAsync(IWiredContext ctx, CancellationToken ct);
}
