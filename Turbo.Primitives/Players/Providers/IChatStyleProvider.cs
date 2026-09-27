using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Players.Providers;

public interface IChatStyleProvider
{
    /// <summary>The style the client knows by this id, or null when the table has no such style.</summary>
    public ChatStyleSnapshot? GetChatStyle(int clientStyleId);

    public Task ReloadAsync(CancellationToken ct);
}
