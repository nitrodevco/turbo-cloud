using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Core.Networking.Encryption;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;

namespace Turbo.Core.Networking.Session;

public interface ISessionContext : IAppSession
{
    public bool PolicyDone { get; set; }
    public string RevisionId { get; }
    public IRc4Service Rc4Service { get; }
    public void SetRevisionId(string revisionId);
    public void SetupEncryption(byte[] key);
    public ValueTask EnqueuePacketAsync(IClientPacket packet, CancellationToken ct = default);
    public Task ProcessPacketBatchAsync(
        IReadOnlyList<IClientPacket> batch,
        CancellationToken ct = default
    );
    public Task SendComposerAsync(IComposer composer, CancellationToken ct = default);
}
