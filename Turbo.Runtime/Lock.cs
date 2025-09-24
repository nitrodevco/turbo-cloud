using System;

namespace Turbo.Runtime;

/// <summary>
/// Lightweight lock token used as an explicit lock target instead of creating many `new object()` instances.
/// It's intentionally minimal: consumers use `lock (theLock) { ... }`.
/// </summary>
public sealed class Lock
{
    // No state required; instances are reference types suitable for 'lock' statements.
}
