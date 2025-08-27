using System;

namespace Turbo.Core.Networking.Protocol;

public record Package(PackageType Type, IClientPacket? Client)
{
    public static Package Policy() => new(PackageType.Policy, null);

    public static Package From(IClientPacket pkt) => new(PackageType.Client, pkt);
}
