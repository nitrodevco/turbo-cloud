using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;

namespace Turbo.Networking.Session;

public class PackageBatchProcessor(ILogger<PackageBatchProcessor> logger) : IPackageBatchProcessor
{
    private readonly ILogger<PackageBatchProcessor> _logger = logger;

    public async ValueTask ProcessBatchAsync(
        ISessionContext ctx,
        IReadOnlyList<Package> batch,
        CancellationToken ct
    )
    {
        foreach (var pkg in batch)
        {
            await HandleOneAsync(ctx, pkg, ct);
        }
    }

    private Task HandleOneAsync(ISessionContext ctx, Package pkg, CancellationToken ct)
    {
        // switch on p.Opcode; call existing handlers
        return Task.CompletedTask;
    }
}
