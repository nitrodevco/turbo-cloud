using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Connection;
using SuperSocket.Server;
using Turbo.Networking.Abstractions.Encryption;
using Turbo.Networking.Abstractions.Session;
using Turbo.Networking.Configuration;
using Turbo.Networking.Encryption;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Session;

public class SessionContext(PacketProcessor packetProcessor) : AppSession(), ISessionContext
{
    private readonly PacketProcessor _packetProcessor = packetProcessor;

    public bool PolicyDone { get; set; } = true;
    public string RevisionId { get; private set; } = "Default";
    public long PlayerId { get; private set; }
    public IStreamCipher Rc4Engine { get; private set; }

    public void SetRevisionId(string revisionId)
    {
        RevisionId = revisionId;
    }

    public void SetPlayerId(long playerId)
    {
        PlayerId = playerId;
    }

    public void SetupEncryption(byte[] key)
    {
        Rc4Engine = new Rc4Engine(new KeyParameter(key));
    }

    protected override async ValueTask OnSessionConnectedAsync()
    {
        await base.OnSessionConnectedAsync();

        Console.WriteLine($"Session context created: {SessionID}");
    }

    protected override async ValueTask OnSessionClosedAsync(CloseEventArgs e)
    {
        await base.OnSessionClosedAsync(e);

        Console.WriteLine($"Session context closed: {SessionID}");
    }

    public async Task SendComposerAsync(IComposer composer, CancellationToken ct = default)
    {
        await _packetProcessor.ProcessComposer(this, composer, ct);
    }
}
