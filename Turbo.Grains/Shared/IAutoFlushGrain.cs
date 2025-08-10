using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Grains.Shared;

public interface IAutoFlushGrain
{
    public bool IsDirty { get; }
    public void AcceptChanges();
    public Task FlushExternalAsync(CancellationToken ct);
}