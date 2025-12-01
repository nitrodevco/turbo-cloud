using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Packets;

public interface ISerializer
{
    public int Header { get; }

    public IServerPacket Serialize(IComposer message);
}
