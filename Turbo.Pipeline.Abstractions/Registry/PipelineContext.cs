using System;
using System.Collections.Generic;

namespace Turbo.Pipeline.Abstractions.Registry;

public abstract class PipelineContext
{
    public required IServiceProvider Services { get; init; }
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public IDictionary<string, object?> Items { get; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    public bool IsAborted { get; private set; }
    public string? AbortReason { get; private set; }

    public void Abort(string? reason = null)
    {
        IsAborted = true;
        AbortReason = reason;
    }
}
