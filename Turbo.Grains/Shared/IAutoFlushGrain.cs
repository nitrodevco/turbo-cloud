using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Grains.Shared;

public interface IAutoFlushGrain
{
    bool IsDirty { get; }
    Task FlushExternalAsync(CancellationToken ct);
    void AcceptChanges();
}