using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Plugins.Messages;

/// <summary>
/// The session a plugin message came from, as its <see cref="IPluginMessageHandler{T}"/> sees it.
/// The host implements it over the packet's own context.
/// </summary>
public abstract class PluginMessageContext
{
    /// <summary>The signed-in player; 0 only for a handler that allows anonymous sessions.</summary>
    public abstract PlayerId PlayerId { get; }

    /// <summary>The room the session is in, -1 when none.</summary>
    public abstract RoomId RoomId { get; }

    public abstract SessionKey SessionKey { get; }

    /// <summary>Sends <paramref name="message"/> back to the session the message came from.</summary>
    public abstract Task ReplyAsync(IPluginMessage message, CancellationToken ct);
}
