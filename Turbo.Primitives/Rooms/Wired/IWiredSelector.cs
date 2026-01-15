using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredSelector : IWiredBox
{
    public bool GetIsFilter();
    public bool GetIsInvert();
    public Task<IWiredSelectionSet> SelectAsync(IWiredProcessingContext ctx, CancellationToken ct);
}
