using System;

namespace Turbo.Core.Packets;

public record IPacketListener
{
    public Delegate Action { get; init; }
    public WeakReference Sender { get; init; }
    public Type Type { get; init; }
}