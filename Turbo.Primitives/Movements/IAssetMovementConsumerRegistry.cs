using System;

namespace Turbo.Primitives.Movements;

/// <summary>
/// Where plugins register an <see cref="IAssetMovementConsumer"/>, the way they register
/// extension packets (<c>docs/plugin-packets.md</c>): take it from the host services in
/// <c>StartAsync</c>, register, and dispose the handle in <c>StopAsync</c>. The registry removes
/// nothing itself. While no consumer is registered the outbox is not read at all.
/// </summary>
public interface IAssetMovementConsumerRegistry
{
    /// <summary>
    /// Starts delivering to <paramref name="consumer"/> from its own cursor. Disposing the handle
    /// stops it and cancels a batch it is handling.
    /// </summary>
    /// <exception cref="ArgumentException">The consumer has no name.</exception>
    /// <exception cref="InvalidOperationException">A consumer with that name is registered.</exception>
    IDisposable Register(IAssetMovementConsumer consumer);
}
