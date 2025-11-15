using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Packets;

public interface ISerializer
{
    public int Header { get; }

    public IServerPacket Serialize(IComposer message);
}
