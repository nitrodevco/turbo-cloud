namespace Turbo.Networking.Session;

using System;
using System.Threading.Tasks;

using DotNetty.Transport.Channels;

using Turbo.Core.Networking.Encryption;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;

public class SessionContext(IChannelHandlerContext ctx) : ISessionContext
{
    private readonly IChannelHandlerContext _ctx = ctx;

    public IChannel Channel => _ctx.Channel;

    public IChannelId ChannelId => _ctx.Channel.Id;

    public string RevisionId { get; private set; }

    public long PlayerId { get; private set; }

    public bool IsAuthenticated => PlayerId != 0;

    public IRc4Service Rc4 { get; set; }

    public void SetRevision(string revisionId)
    {
        RevisionId = revisionId;
    }

    public void AttachPlayer(long playerId)
    {
        PlayerId = playerId;
    }

    public async Task DisposeAsync()
    {
        await _ctx.CloseAsync();
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> payload)
    {
        // TODO: write to TCP/WS
        return ValueTask.CompletedTask;
    }

    public void PauseReads() => Channel.Configuration.SetOption(ChannelOption.AutoRead, false);

    public void ResumeReads() => Channel.Configuration.SetOption(ChannelOption.AutoRead, true);
}
