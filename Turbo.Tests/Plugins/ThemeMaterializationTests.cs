using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Files;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Settings;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

[Trait("evidence", "inferred")]
public sealed class ThemeMaterializationTests : IDisposable
{
    private static readonly PlayerId PLAYER = PlayerId.Parse(101);
    private readonly ThemeQueries _queries = new();
    private readonly SqliteDb _db;
    private readonly Fakes _fakes = new();
    private readonly LoadedPluginGamedata _loaded = new();
    private readonly IThemeClientState _clients = new ThemeClientState();
    private readonly GamedataFileService _files;
    private readonly IDisposable _theme;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ThemeMaterializationTests()
    {
        _db = new SqliteDb(_queries);
        _files = new GamedataFileService(
            _db,
            Options.Create(new GamedataConfig()),
            new FurnitureOfferCatalog(
                _fakes.Create<ICatalogSnapshotProvider<NormalCatalog>>(),
                _fakes.Create<ICatalogSnapshotProvider<BuildersClubCatalog>>()
            ),
            _fakes.Create<IServerSettings>(),
            _fakes.Create<IAssetBundleStore>(),
            TimeProvider.System,
            _loaded,
            NullLogger<GamedataFileService>.Instance
        );
        _theme = _loaded.Add("night", () => _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES));
        _loaded.SetTheme("night", true, "habbo");
        Patch("v1");
        _db.Insert(
            new GamedataThemeEntity
            {
                PluginKey = "night",
                Status = ThemeStatus.Development,
                AudiencePlayers = "[101]",
                UpdatedAt = DateTime.UtcNow,
            }
        );
    }

    public void Dispose()
    {
        _theme.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task A_cold_session_materializes_theme_variables_once_and_reuses_the_build()
    {
        // Build linked files/stamps first, leaving theme materialization cold. Count actual SQL,
        // excluding schema setup and unrelated gamedata builds from the measurement.
        await _files.GetCurrentAsync(GamedataFiles.EXTERNAL_VARIABLES, Ct);
        _queries.Commands.Clear();
        var audiences = Audiences(_files);

        var session = await audiences.SessionAsync(PLAYER, null, Ct);

        session.Theme.Should().Be("night");
        _queries
            .Reads("gamedata_variables")
            .Should()
            .Be(1, "session variables and tracked revision must use one materialization");
        _queries
            .Reads("gamedata_themes")
            .Should()
            .Be(2, "one audience read and one materialization read");
        _queries.Commands.Clear();
        var look = await _files.ThemeLookAsync("night", Ct);
        var variables = await _files.ThemeVariablesAsync("night", Ct);
        _queries
            .Reads("gamedata_variables")
            .Should()
            .Be(0, "both projections reuse the session's build");
        _queries.Reads("gamedata_themes").Should().Be(0);
        variables.Should().BeSameAs(session.Variables);
        _clients.DrawnBy(PLAYER)!.Revision.Should().Be(look.Revision);
    }

    [Fact]
    public async Task Session_variables_and_tracked_revision_come_from_one_build_when_invalidated_between_reads()
    {
        async Task<IReadOnlyDictionary<string, string>?> VariablesThenChangeAsync()
        {
            var variables = await _files.ThemeVariablesAsync("night", Ct);
            Patch("v2");
            return variables;
        }
        async Task<ThemeView> ViewThenChangeAsync()
        {
            var view = await _files.ThemeViewAsync("night", Ct);
            Patch("v2");
            return view;
        }
        _fakes.Handlers["ThemeViewAsync"] = _ => ViewThenChangeAsync();
        _fakes.Handlers["ThemeVariablesAsync"] = _ => VariablesThenChangeAsync();
        _fakes.Handlers["ThemeLookAsync"] = _ => _files.ThemeLookAsync("night", Ct);
        var audiences = Audiences(_fakes.Create<IThemeFileService>());

        var session = await audiences.SessionAsync(PLAYER, null, Ct);
        var supplied = _files.FindLook(_clients.DrawnBy(PLAYER)!.Revision)!;

        session.Variables![ExternalVariablesFile.TEMPLATE_PATCHES].Should().Contain("/v1/");
        supplied
            .Variables[ExternalVariablesFile.TEMPLATE_PATCHES]
            .Should()
            .Be(
                session.Variables[ExternalVariablesFile.TEMPLATE_PATCHES],
                "tracking must describe the variables this client actually received"
            );
        _fakes.Log.Of("ThemeViewAsync").Should().ContainSingle();
        _fakes.Log.Of("ThemeVariablesAsync").Should().BeEmpty();
        _fakes.Log.Of("ThemeLookAsync").Should().BeEmpty();
        (await _files.ThemeLookAsync("night", Ct))
            .Variables[ExternalVariablesFile.TEMPLATE_PATCHES]
            .Should()
            .Contain("/v2/");
    }

    [Fact]
    public async Task Session_projection_preserves_default_nulls_and_excludes_the_look_s_text_url()
    {
        _db.Insert(
            new GamedataVariableEntity
            {
                Key = ExternalVariablesFile.EXTERNAL_TEXTS_URL,
                Value = "\"/texts/v1.txt\"",
            }
        );
        var custom = await _files.ThemeViewAsync("night", Ct);
        custom.SessionVariables!.Keys.Should().BeEquivalentTo(ExternalVariablesFile.MERGED);
        custom.SessionVariables.Should().NotContainKey(ExternalVariablesFile.EXTERNAL_TEXTS_URL);
        custom
            .Look.Variables[ExternalVariablesFile.EXTERNAL_TEXTS_URL]
            .Should()
            .Be("\"/texts/v1.txt\"");
        foreach (var (key, value) in custom.SessionVariables)
            custom.Look.Variables[key].Should().Be(value);
        (await _files.ThemeVariablesAsync("night", Ct)).Should().BeSameAs(custom.SessionVariables);
        (await _files.ThemeLookAsync("night", Ct)).Should().BeSameAs(custom.Look);

        await MakeLiveAsync(isDefault: true);
        var defaultView = await _files.ThemeViewAsync("night", Ct);
        defaultView.SessionVariables.Should().BeNull();
        (await _files.ThemeVariablesAsync("night", Ct)).Should().BeNull();
        defaultView.Look.Variables[ExternalVariablesFile.TEMPLATE_PATCHES].Should().Contain("/v1/");
        defaultView
            .Look.Variables[ExternalVariablesFile.EXTERNAL_TEXTS_URL]
            .Should()
            .Be("\"/texts/v1.txt\"");
        _files.FindLook(defaultView.Look.Revision).Should().NotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Null_and_empty_named_chains_remain_distinct_in_either_cache_order(
        bool emptyFirst
    )
    {
        await MakeLiveAsync(isDefault: false);
        var first = await _files.ThemeViewAsync(emptyFirst ? "" : null, Ct);
        var second = await _files.ThemeViewAsync(emptyFirst ? null : "", Ct);
        var combined = emptyFirst ? second : first;
        var empty = emptyFirst ? first : second;
        combined
            .SessionVariables.Should()
            .BeNull("without an explicit default, null is the default file's combined Live look");
        combined.Look.Variables[ExternalVariablesFile.TEMPLATE_PATCHES].Should().Contain("/v1/");
        empty.SessionVariables.Should().NotBeNull();
        empty.Look.Variables[ExternalVariablesFile.TEMPLATE_PATCHES].Should().Be("{}");
        empty.Look.Revision.Should().NotBe(combined.Look.Revision);
    }

    [Fact]
    public async Task Case_variants_share_one_build_and_invalidation_rebuilds_both_projections()
    {
        var first = await _files.ThemeViewAsync("night", Ct);
        (await _files.ThemeViewAsync("NIGHT", Ct)).Should().BeSameAs(first);
        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        var unchanged = await _files.ThemeViewAsync("night", Ct);
        unchanged.Should().NotBeSameAs(first);
        unchanged.Look.Revision.Should().Be(first.Look.Revision);
        Patch("v2");
        var changed = await _files.ThemeViewAsync("night", Ct);
        changed.Look.Revision.Should().NotBe(first.Look.Revision);
        changed.SessionVariables![ExternalVariablesFile.TEMPLATE_PATCHES].Should().Contain("/v2/");
        changed
            .Look.Variables[ExternalVariablesFile.TEMPLATE_PATCHES]
            .Should()
            .Be(changed.SessionVariables[ExternalVariablesFile.TEMPLATE_PATCHES]);
        (await _files.ThemeLookAsync("night", Ct)).Should().BeSameAs(changed.Look);
        (await _files.ThemeVariablesAsync("night", Ct)).Should().BeSameAs(changed.SessionVariables);
    }

    [Fact]
    public async Task A_text_url_change_updates_the_revision_without_adding_it_to_session_variables()
    {
        _db.Insert(
            new GamedataVariableEntity
            {
                Key = ExternalVariablesFile.EXTERNAL_TEXTS_URL,
                Value = "\"/texts/v1.txt\"",
            }
        );
        var before = await _files.ThemeViewAsync("night", Ct);
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            var row = await db.GamedataVariables.SingleAsync(
                x => x.Key == ExternalVariablesFile.EXTERNAL_TEXTS_URL,
                Ct
            );
            row.Value = "\"/texts/v2.txt\"";
            await db.SaveChangesAsync(Ct);
        }
        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        var after = await _files.ThemeViewAsync("night", Ct);
        after.Look.Revision.Should().NotBe(before.Look.Revision);
        after.SessionVariables.Should().BeEquivalentTo(before.SessionVariables);
        after
            .Look.Variables[ExternalVariablesFile.EXTERNAL_TEXTS_URL]
            .Should()
            .Be("\"/texts/v2.txt\"");
        _files.FindLook(before.Look.Revision).Should().BeSameAs(before.Look);
    }

    [Fact]
    public async Task Variables_only_builds_do_not_evict_looks_and_are_retained_when_first_requested_as_a_view()
    {
        var first = await _files.ThemeLookAsync("look-0", Ct);
        for (var index = 1; index < 64; index++)
            await _files.ThemeLookAsync($"look-{index}", Ct);
        await _files.ThemeVariablesAsync("variables-only", Ct);
        _files
            .FindLook(first.Revision)
            .Should()
            .BeSameAs(first, "variables-only reads must not consume retained revision slots");
        _queries.Commands.Clear();
        var view = await _files.ThemeViewAsync("variables-only", Ct);
        _queries
            .Reads("gamedata_variables")
            .Should()
            .Be(0, "a variables-only build is already materialized");
        _files.FindLook(view.Look.Revision).Should().BeSameAs(view.Look);
        _files
            .FindLook(first.Revision)
            .Should()
            .BeNull("only the newest 64 revisioned looks are retained");
        (await _files.ThemeLookAsync("look-0", Ct)).Should().BeSameAs(first);
        _files
            .FindLook(first.Revision)
            .Should()
            .BeNull("a previously remembered cache hit keeps the existing eviction behavior");
    }

    [Fact]
    public async Task Failed_materialization_preserves_retained_looks_and_retries_the_new_build()
    {
        var before = await _files.ThemeViewAsync("night", Ct);
        Patch("v2");
        var failure = new InvalidOperationException("materialization read failed");
        _queries.FailNextVariables = failure;
        var build = () => _files.ThemeViewAsync("night", Ct);
        (await build.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should()
            .BeSameAs(failure);
        _files.FindLook(before.Look.Revision).Should().BeSameAs(before.Look);
        var retry = await _files.ThemeViewAsync("night", Ct);
        retry.Look.Variables[ExternalVariablesFile.TEMPLATE_PATCHES].Should().Contain("/v2/");
        retry
            .SessionVariables![ExternalVariablesFile.TEMPLATE_PATCHES]
            .Should()
            .Be(retry.Look.Variables[ExternalVariablesFile.TEMPLATE_PATCHES]);
    }

    [Fact]
    public async Task Cancelled_materialization_can_be_retried_without_a_partial_cache_entry()
    {
        await _files.GetCurrentAsync(GamedataFiles.EXTERNAL_VARIABLES, Ct);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await cancelled.CancelAsync();
        var build = () => _files.ThemeViewAsync("night", cancelled.Token);
        await build.Should().ThrowAsync<OperationCanceledException>();
        _queries.Commands.Clear();
        var retry = await _files.ThemeViewAsync("night", Ct);
        _queries.Reads("gamedata_variables").Should().Be(1);
        retry.SessionVariables![ExternalVariablesFile.TEMPLATE_PATCHES].Should().Contain("/v1/");
    }

    private async Task MakeLiveAsync(bool isDefault)
    {
        await using var db = await _db.CreateDbContextAsync(Ct);
        var row = await db.GamedataThemes.SingleAsync(x => x.PluginKey == "night", Ct);
        row.Status = ThemeStatus.Live;
        row.IsDefault = isDefault;
        await db.SaveChangesAsync(Ct);
        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
    }

    private ThemeAudienceService Audiences(IThemeFileService files) =>
        new(
            _db,
            files,
            _loaded,
            _clients,
            _fakes.Create<IGrainFactory>(),
            TimeProvider.System,
            NullLogger<ThemeAudienceService>.Instance
        );

    private void Patch(string version)
    {
        _loaded.SetParts(
            "night",
            new Dictionary<string, string>
            {
                [ExternalVariablesFile.TEMPLATE_PATCHES] =
                    $$"""{ "night": "/night/{{version}}/patches.json" }""",
            }
        );
        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
    }

    private sealed class ThemeQueries : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public Exception? FailNextVariables { get; set; }

        public int Reads(string table) =>
            Commands.Count(x => x.Contains('"' + table + '"', StringComparison.Ordinal));

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            Commands.Add(command.CommandText);
            if (
                command.CommandText.Contains("gamedata_variables", StringComparison.Ordinal)
                && FailNextVariables is { } failure
            )
            {
                FailNextVariables = null;
                throw failure;
            }
            return ValueTask.FromResult(result);
        }
    }
}
