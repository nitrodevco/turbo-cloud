using System;
using Orleans;

namespace Turbo.Admin.Common;

/// <summary>
/// Holds the currently-connected <see cref="IClusterClient"/>, if any. The Orleans connector (in the hosting
/// project) swaps this out each time it (re)builds and connects a fresh client. Endpoints that can tolerate the
/// emulator being offline should check <see cref="Current"/> directly instead of taking a DI-injected
/// <see cref="IClusterClient"/>/<see cref="IGrainFactory"/> parameter, since resolving those throws before the
/// endpoint body even runs when disconnected - too late to degrade gracefully.
/// </summary>
public sealed class GrainClientHolder
{
    public IClusterClient? Current { get; set; }

    public IClusterClient Require() =>
        Current ?? throw new InvalidOperationException("Orleans client is not connected.");
}
