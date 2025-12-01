using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Packets;

public interface IParser
{
    public IMessageEvent Parse(IClientPacket packet);
}
