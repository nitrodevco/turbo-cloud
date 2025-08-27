using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Core.Networking.Protocol;

namespace Turbo.Core.Networking.Session;

public interface IPackageBatchProcessor
{
    public ValueTask ProcessBatchAsync(
        ISessionContext ctx,
        IReadOnlyList<Package> batch,
        CancellationToken ct
    );
}
