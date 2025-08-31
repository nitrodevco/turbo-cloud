using System;
using System.Collections.Generic;

namespace Turbo.Core.Events.Registry;

public class EventContext
{
    public required IServiceProvider Services { get; init; }
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
    public IDictionary<string, object?> Items { get; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);
}
