using System;
using Microsoft.Extensions.Logging;
using Turbo.Core.Networking.Behaviors;
using Turbo.Core.Networking.Pipeline;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Revisions;

namespace Turbo.Networking.Behaviors;

public class PacketContext(ISessionContext sessionCtx, ILogger logger, IngressToken ingressToken)
    : IPacketContext
{
    public ISessionContext SessionContext { get; } = sessionCtx;
    public ILogger Logger { get; } = logger;
    public IngressToken IngressToken { get; } = ingressToken;
}
