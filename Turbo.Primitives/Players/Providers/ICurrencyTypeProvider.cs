using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Players.Providers;

public interface ICurrencyTypeProvider
{
    public CurrencyTypeSnapshot? GetCurrencyType(int typeId);

    public Task ReloadAsync(CancellationToken ct);
}
