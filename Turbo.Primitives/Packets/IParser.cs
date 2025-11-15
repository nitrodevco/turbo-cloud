using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Packets;

public interface IParser
{
    public IMessageEvent Parse(IClientPacket packet);
}
