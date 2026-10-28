using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Turbo.Database.Context;
using Turbo.Gamedata;
using Turbo.Gamedata.Files;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Settings;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

public sealed class ThemeFileProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void The_default_file_and_theme_views_share_one_cache_and_invalidation_event()
    {
        var fakes = new Fakes();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        new GamedataModule().ConfigureServices(builder.Services, builder);
        builder.Services.AddLogging();
        builder.Services.AddSingleton(fakes.Create<IDbContextFactory<TurboDbContext>>());
        builder.Services.AddSingleton(fakes.Create<IServerSettings>());
        builder.Services.AddSingleton(fakes.Create<IAssetBundleStore>());
        builder.Services.AddSingleton(fakes.Create<ICatalogSnapshotProvider<NormalCatalog>>());
        builder.Services.AddSingleton(
            fakes.Create<ICatalogSnapshotProvider<BuildersClubCatalog>>()
        );
        using var host = builder.Services.BuildServiceProvider();

        var files = host.GetRequiredService<IGamedataFileService>();
        var themes = host.GetRequiredService<IThemeFileService>();
        files.Should().BeOfType<GamedataFileService>();
        files.Should().BeSameAs(themes);
        var invalidated = new List<string>();
        themes.Invalidated += invalidated.Add;

        files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);

        invalidated.Should().Equal(GamedataFiles.EXTERNAL_VARIABLES);
    }

    [Fact]
    public async Task An_alternate_provider_serves_sessions_and_live_reload_without_concrete_casts()
    {
        using var db = new SqliteDb();
        var fakes = new Fakes();
        var variables = new Dictionary<string, string> { ["custom"] = "provider" };
        var look = new ThemeLook("", "alternate-revision", variables);
        var subscribers = new List<Action<string>>();
        fakes.Handlers["ThemeVariablesAsync"] = _ =>
            Task.FromResult<IReadOnlyDictionary<string, string>?>(variables);
        fakes.Handlers["ThemeLookAsync"] = _ => Task.FromResult(look);
        fakes.Handlers["FindLook"] = _ => look;
        fakes.Handlers["add_Invalidated"] = call =>
        {
            subscribers.Add((Action<string>)call.Args[0]!);
            return null;
        };
        fakes.Handlers["remove_Invalidated"] = call =>
        {
            subscribers.Remove((Action<string>)call.Args[0]!);
            return null;
        };
        fakes.Handlers["Invalidate"] = call =>
        {
            foreach (var subscriber in subscribers.ToArray())
                subscriber((string)call.Args[0]!);
            return null;
        };
        var replacement = fakes.Create<IThemeFileService>();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        new GamedataModule().ConfigureServices(builder.Services, builder);
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IDbContextFactory<TurboDbContext>>(db);
        builder.Services.AddSingleton(fakes.Create<IGrainFactory>());
        builder.Services.AddSingleton(fakes.Create<IHostApplicationLifetime>());
        builder.Services.AddSingleton(replacement);
        using var host = builder.Services.BuildServiceProvider();

        host.GetRequiredService<IGamedataFileService>().Should().BeSameAs(replacement);
        var audiences = host.GetRequiredService<IThemeAudiences>();
        var session = await audiences.SessionAsync(PlayerId.Parse(101), null, Ct);
        session.Variables.Should().BeSameAs(variables);
        host.GetRequiredService<IThemeClientState>()
            .DrawnBy(PlayerId.Parse(101))!
            .Revision.Should()
            .Be(look.Revision, "the default session provider uses the host's shared tracking");

        var live = host.GetRequiredService<IThemeLiveReload>();
        live.Find(look.Revision).Should().BeSameAs(look);
        var hosted = (IHostedService)live;
        await hosted.StartAsync(Ct);
        subscribers.Should().ContainSingle();
        replacement.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        await hosted.StopAsync(Ct);
        subscribers.Should().BeEmpty();
        var reads = fakes.Log.Of("ThemeLookAsync").Count();

        await live.SettleAsync(Ct);

        fakes.Log.Of("ThemeLookAsync").Count().Should().BeGreaterThan(reads);
    }
}
