using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Catalog.Providers;

public interface IBonusRareProvider
{
    Task<BonusRareSnapshot> GetInfoAsync(PlayerId playerId, CancellationToken ct);
}
