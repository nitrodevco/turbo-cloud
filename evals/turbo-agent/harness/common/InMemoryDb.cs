using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Turbo.Database.Context;

namespace EvalHarness;

/// <summary>
/// An EF Core InMemory TurboDbContext factory. Row-limiting queries without an ORDER BY throw,
/// so a nondeterministic "Take" is a test failure rather than a log line.
/// </summary>
public sealed class InMemoryDb : IDbContextFactory<TurboDbContext>
{
    private readonly DbContextOptions<TurboDbContext> _options;

    public InMemoryDb(bool throwOnUnorderedTake = false)
    {
        var b = new DbContextOptionsBuilder<TurboDbContext>()
            .UseInMemoryDatabase("eval-" + Guid.NewGuid());
        b.ConfigureWarnings(w =>
        {
            if (throwOnUnorderedTake)
            {
                w.Throw(CoreEventId.RowLimitingOperationWithoutOrderByWarning);
                w.Throw(CoreEventId.FirstWithoutOrderByAndFilterWarning);
            }
            w.Ignore(InMemoryEventId.TransactionIgnoredWarning);
        });
        _options = b.Options;
    }

    public TurboDbContext CreateDbContext() => new(_options);

    public Task<TurboDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
        Task.FromResult(CreateDbContext());
}
