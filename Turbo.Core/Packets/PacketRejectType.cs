namespace Turbo.Core.Packets;

public enum PacketRejectType
{
    None,
    RateLimited,
    Busy,
    ServerBusy,
}
