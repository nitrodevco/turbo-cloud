namespace Turbo.Core.Packets.Messages;

public interface ISerializer
{
    public int Header { get; }

    public IServerPacket Serialize(IComposer message);
}
