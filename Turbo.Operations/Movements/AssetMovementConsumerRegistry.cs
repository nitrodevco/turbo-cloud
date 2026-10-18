using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;

namespace Turbo.Operations.Movements;

/// <summary>
/// The consumers plugins registered, per silo: each silo loads the same plugins, and its
/// dispatcher activation reads the outbox for them. Registering one wakes the dispatcher.
/// </summary>
internal sealed class AssetMovementConsumerRegistry(
    IGrainFactory grainFactory,
    ILogger<AssetMovementConsumerRegistry> logger
) : IAssetMovementConsumerRegistry
{
    private readonly ConcurrentDictionary<string, Registration> _consumers = new(
        StringComparer.Ordinal
    );

    /// <summary>The registrations now, by name.</summary>
    public ImmutableArray<Registration> Current =>
        [.. _consumers.Values.OrderBy(x => x.Consumer.Name, StringComparer.Ordinal)];

    public IDisposable Register(IAssetMovementConsumer consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer.Name, nameof(consumer));

        var registration = new Registration(consumer, this);

        if (!_consumers.TryAdd(consumer.Name, registration))
            throw new InvalidOperationException(
                $"An asset movement consumer named \"{consumer.Name}\" is registered."
            );

        grainFactory
            .GetAssetMovementDispatcherGrain()
            .WakeAsync(CancellationToken.None)
            .LogAndForget(
                logger,
                "wake the asset movement dispatcher for {Consumer}",
                consumer.Name
            );

        return registration;
    }

    private void Remove(Registration registration) =>
        // Only while it is still the one registered under its name.
        _consumers.TryRemove(
            new KeyValuePair<string, Registration>(registration.Consumer.Name, registration)
        );

    /// <summary>
    /// One registration of a consumer. Disposing it stops delivery and cancels the batch the
    /// consumer is handling; registering the same consumer again starts over from its cursor.
    /// </summary>
    internal sealed class Registration : IDisposable
    {
        private readonly AssetMovementConsumerRegistry _owner;
        private readonly CancellationTokenSource _stopping = new();
        private int _disposed;

        public Registration(IAssetMovementConsumer consumer, AssetMovementConsumerRegistry owner)
        {
            Consumer = consumer;
            _owner = owner;
        }

        public IAssetMovementConsumer Consumer { get; }

        /// <summary>Cancelled when the registration is disposed.</summary>
        public CancellationToken Stopping => _stopping.Token;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            _owner.Remove(this);
            _stopping.Cancel();
        }
    }
}
