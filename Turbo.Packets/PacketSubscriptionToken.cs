using System;
using System.Threading;
using Turbo.Core.Packets;

namespace Turbo.Packets;

public class PacketSubscriptionToken(IPacketMessageHub hub, Type messageType, Guid id) : IDisposable
{
    private readonly IPacketMessageHub _hub = hub;
    private readonly Type _messageType = messageType;
    private readonly Guid _id = id;
    private int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _hub.RemoveListenerById(_messageType, _id);
    }
}
