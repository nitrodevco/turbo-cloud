using System.IO;
using FluentAssertions;
using Turbo.Primitives.Plugins;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// A plugin's database context is scoped, and its routes and tasks are built once as it loads. A
/// route taking the context by constructor would hold one context for every request (or, as the
/// plugin's service provider validates scopes, fail to load with the service provider's own words):
/// the load failure tells the author what to take instead, and the way docs/database.md shows
/// loads. A plugin author's first plugin asked which of the two it gets (stranger test S1, point 5).
/// </summary>
public sealed class PluginScopedContextTests : IDisposable
{
    private const string CONTEXT = """
        using System.ComponentModel.DataAnnotations.Schema;
        using Microsoft.EntityFrameworkCore;
        using Turbo.Database.Context;

        namespace Values;

        public sealed class ValuesDb : PluginDbContextBase<ValuesDb>
        {
            public DbSet<ValueRow> Values => Set<ValueRow>();
        }

        [Table("values")]
        public sealed class ValueRow
        {
            public int Id { get; set; }
        }
        """;

    private const string ROUTE_TAKING_THE_CONTEXT = """
        using System.Threading;
        using System.Threading.Tasks;
        using Turbo.Contracts.Plugins.Web;

        namespace Values;

        public sealed class ValuesRoute(ValuesDb db) : IPluginWebRoute
        {
            public string Method => "GET";

            public string Pattern => "values";

            public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct) =>
                Task.FromResult(PluginWebResult.Ok(new { db.ContextId }));
        }
        """;

    // docs/database.md, "From a route, a task or a singleton".
    private const string ROUTE_TAKING_A_SCOPE_FACTORY = """
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.EntityFrameworkCore;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins.Web;

        namespace Values;

        public sealed class ValuesRoute(IServiceScopeFactory scopes) : IPluginWebRoute
        {
            public string Method => "GET";

            public string Pattern => "values";

            public async Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct)
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ValuesDb>();

                return PluginWebResult.Ok(new { count = await db.Values.CountAsync(ct) });
            }
        }
        """;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-scoped-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task A_route_taking_the_plugins_context_does_not_load_and_is_told_to_take_a_scope_factory()
    {
        var status = await LoadAsync(ROUTE_TAKING_THE_CONTEXT);

        status.State.Should().Be(PluginStateType.Failed);
        status.Error.Should().Contain("ValuesDb").And.Contain("IServiceScopeFactory");
    }

    [Fact]
    public async Task A_route_taking_a_scope_factory_loads()
    {
        var status = await LoadAsync(ROUTE_TAKING_A_SCOPE_FACTORY);

        status.State.Should().Be(PluginStateType.Loaded, status.Error);
    }

    private async Task<PluginSnapshot> LoadAsync(string route)
    {
        PluginBuilder.Write(_folder, "values", [CONTEXT, route]);

        var manager = new PluginTestHost(_db, _fakes).Manager(_folder);

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();

        await manager.UnloadAllAsync(Ct);

        return status;
    }
}
