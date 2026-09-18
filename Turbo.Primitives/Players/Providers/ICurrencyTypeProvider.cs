using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Primitives.Players.Providers;

public interface ICurrencyTypeProvider
{
    public CurrencyTypeSnapshot? GetCurrencyType(int typeId);

    /// <summary>The currency type row for a kind, or false when no such currency is configured.</summary>
    public bool TryGetCurrencyTypeId(CurrencyKind kind, out int typeId);

    public Task ReloadAsync(CancellationToken ct);
}
