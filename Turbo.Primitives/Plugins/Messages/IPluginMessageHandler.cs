using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Plugins.Messages;

/// <summary>
/// Handles a plugin message a client sent. Found in a plugin's assembly as a packet handler is, and
/// gated the same way: only a signed-in session reaches it unless it carries
/// <c>[AllowAnonymous]</c>, and <c>[RequiresPermission]</c> is honoured. Keep it orchestration
/// only, as a packet handler.
/// </summary>
public interface IPluginMessageHandler<in T>
    where T : IPluginMessage
{
    ValueTask HandleAsync(T message, PluginMessageContext ctx, CancellationToken ct);
}
