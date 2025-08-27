using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;

namespace Turbo.Core.Networking.Session;

public interface ISessionContext : IAppSession
{
    public ValueTask EnqueueAsync(Package package, CancellationToken ct);
}
