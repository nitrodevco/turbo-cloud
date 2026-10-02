using System.Collections.Generic;
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

    /// <summary>
    /// The kind of the enabled currency type with this name, ignoring case: how an operator
    /// names a currency, since the types are rows a hotel adds to.
    /// </summary>
    public bool TryGetCurrencyKindByName(string name, out CurrencyKind kind);

    /// <summary>The names of the enabled currency types, for a usage line.</summary>
    public IReadOnlyCollection<string> GetEnabledCurrencyNames();

    public Task ReloadAsync(CancellationToken ct);
}
