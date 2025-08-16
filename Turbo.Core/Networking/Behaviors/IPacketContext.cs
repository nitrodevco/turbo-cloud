using System;
using Microsoft.Extensions.Logging;
using Turbo.Core.Networking.Pipeline;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Revisions;

namespace Turbo.Core.Networking.Behaviors;

public interface IPacketContext
{
    public ISessionContext SessionContext { get; }
    public ILogger Logger { get; }
    public IngressToken IngressToken { get; }
}
