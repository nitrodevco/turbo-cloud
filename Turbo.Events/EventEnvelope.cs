using System;
using System.Threading.Tasks;

namespace Turbo.Events;

public record EventEnvelope(
    object Event,
    string? Tag,
    TaskCompletionSource? SyncTcs,
    DateTimeOffset EnqueuedAt
);
