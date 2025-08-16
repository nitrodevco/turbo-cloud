using System;

namespace Turbo.Core.Packets;

public record IPacketListener
{
    public required Guid Id { get; init; }

    public required WeakReference Sender { get; init; }

    public required Delegate Action { get; init; }
}
