using Turbo.Contracts.Abstractions;

namespace Turbo.Packets.Abstractions;

public interface ISerializer
{
    public int Header { get; }

    public IServerPacket Serialize(IComposer message);
}
