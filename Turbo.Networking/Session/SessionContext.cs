using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SuperSocket.Connection;
using SuperSocket.Server;
using Turbo.Core;
using Turbo.Core.Configuration;
using Turbo.Core.Networking.Encryption;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;
using Turbo.Networking.Encryption;

namespace Turbo.Networking.Session;

public class SessionContext : AppSession, ISessionContext
{
    private readonly IPacketProcessor _packetProcessor;
    private IncomingQueue _incomingQueue;

    public bool PolicyDone { get; set; } = true;
    public string RevisionId { get; private set; } = "default";
    public IRc4Service Rc4Service { get; private set; }

    public SessionContext(IEmulatorConfig config, IPacketProcessor packetProcessor)
        : base()
    {
        _packetProcessor = packetProcessor;
        _incomingQueue = new(this, config.Network.IncomingQueue);
    }

    public void SetRevisionId(string revisionId)
    {
        RevisionId = revisionId;
    }

    public void SetupEncryption(byte[] key)
    {
        Rc4Service = new Rc4Service(key);
    }

    protected override async ValueTask OnSessionConnectedAsync()
    {
        _incomingQueue.Start();

        await base.OnSessionConnectedAsync();

        Console.WriteLine($"Session context created: {SessionID}");
    }

    protected override async ValueTask OnSessionClosedAsync(CloseEventArgs e)
    {
        await _incomingQueue.StopAsync();

        await base.OnSessionClosedAsync(e);

        Console.WriteLine($"Session context closed: {SessionID}");
    }

    public async ValueTask EnqueuePacketAsync(IClientPacket packet, CancellationToken ct = default)
    {
        await _incomingQueue.EnqueueAsync(packet, ct);
    }

    public async Task ProcessPacketBatchAsync(
        IReadOnlyList<IClientPacket> batch,
        CancellationToken ct = default
    )
    {
        foreach (var packet in batch)
        {
            await _packetProcessor.ProcessClientPacket(this, packet, ct);
        }
    }

    public async Task SendComposerAsync(IComposer composer, CancellationToken ct = default)
    {
        await _packetProcessor.ProcessComposer(this, composer, ct);
    }
}
