using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Commands;

/// <summary>
/// Sends a player the commands they may use (<c>chat.commands</c>). The presence passes it on only
/// to a session that accepted the extension, so a caller never asks whether it did.
/// </summary>
public interface ICommandTreeService
{
    Task SendAsync(PlayerId playerId, CancellationToken ct);
}
