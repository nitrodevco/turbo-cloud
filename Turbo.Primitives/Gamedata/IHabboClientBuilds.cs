using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// What the hotel builds from the release of Habbo's HTML client it follows (the avatar data, the
/// Variable FX, the texts, the window templates and theme, the chat styles): each bundle as it is,
/// and what the last build said.
/// </summary>
public interface IHabboClientBuilds
{
    public Task<HabboClientBuildSnapshot> GetAsync(CancellationToken ct);
}
