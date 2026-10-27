using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;

namespace Turbo.Messages.PluginMessages;

/// <summary>A plugin message handler's context over the packet's own <see cref="MessageContext"/>.</summary>
internal sealed class HostPluginMessageContext(MessageContext ctx) : PluginMessageContext
{
    public override PlayerId PlayerId => ctx.PlayerId;

    public override RoomId RoomId => ctx.RoomId;

    public override SessionKey SessionKey => ctx.SessionKey;

    // The session that sent a named message learned its header from client.messages, so it is
    // one that accepted it.
    public override Task ReplyAsync(IPluginMessage message, CancellationToken ct) =>
        ctx.SendComposerAsync(PluginMessageComposer.Compose(message), ct);
}
