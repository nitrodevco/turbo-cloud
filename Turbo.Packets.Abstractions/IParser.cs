using Turbo.Primitives;

namespace Turbo.Packets.Abstractions;

public interface IParser
{
    public IMessageEvent Parse(IClientPacket packet);
}
