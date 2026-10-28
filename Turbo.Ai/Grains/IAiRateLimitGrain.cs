using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Ai;

/// <summary>The runs one limit key (<c>plugin:&lt;key&gt;</c>, <c>player:&lt;id&gt;</c>) started in the last minute, for the whole cluster.</summary>
public interface IAiRateLimitGrain : IGrainWithStringKey
{
    /// <summary>Counts a run when fewer than <paramref name="limit"/> started in the last minute; false, counting nothing, when not.</summary>
    Task<bool> TryStartAsync(Guid reservation, int limit, CancellationToken ct);

    /// <summary>Takes back only this run's reservation (another key refused it).</summary>
    Task UndoAsync(Guid reservation, CancellationToken ct);
}
