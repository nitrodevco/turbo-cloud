using System.IO;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata;
using Turbo.Gamedata.ClientModules;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Settings;
using Turbo.Primitives.Texts;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The example in <c>docs/examples/client-modules/hello</c> is the documentation's proof, so it is
/// built and loaded here as a hotel would: its module is listed for clients, and its texts and
/// variables are given to the hotel.
/// </summary>
public sealed class HelloExamplePluginTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-hello-example-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task The_example_plugin_loads_lists_its_module_and_gives_the_hotel_its_texts_and_variables()
    {
        var example = Path.Combine(RepositoryRoot(), "docs", "examples", "client-modules", "hello");
        var key = JsonDocument
            .Parse(await File.ReadAllTextAsync(Path.Combine(example, "manifest.json"), Ct))
            .RootElement.GetProperty("Key")
            .GetString()!;

        PluginBuilder.Write(
            _folder,
            key,
            await File.ReadAllTextAsync(Path.Combine(example, "HelloPlugin.cs"), Ct)
        );

        foreach (var sub in new[] { "wwwroot", "gamedata" })
        foreach (var file in Directory.EnumerateFiles(Path.Combine(example, sub)))
        {
            Directory.CreateDirectory(Path.Combine(_folder, key, sub));
            File.Copy(file, Path.Combine(_folder, key, sub, Path.GetFileName(file)));
        }

        var config = Options.Create(new GamedataConfig());
        var files = _fakes.Create<IGamedataFileService>();
        var writes = new GamedataWriteLock();
        var variables = new GamedataVariableService(
            _db,
            config,
            files,
            writes,
            _fakes.Create<IServerSettings>(),
            new HabboGamedataClient(
                _fakes.Create<IHttpClientFactory>(),
                NullLogger<HabboGamedataClient>.Instance
            ),
            NullLogger<GamedataVariableService>.Instance
        );
        var texts = new GamedataTextService(
            _db,
            config,
            files,
            writes,
            TimeProvider.System,
            _fakes.Create<IHotelTextProvider>(),
            NullLogger<GamedataTextService>.Instance
        );
        var registry = new ClientModuleRegistry(
            new TestOptionsMonitor<GamedataConfig>(new GamedataConfig()),
            NullLogger<ClientModuleRegistry>.Instance
        );
        var manager = new PluginManager(
            new ServiceCollection()
                .AddSingleton<IClientModuleDirectory>(registry)
                .AddSingleton<IPluginGamedataSeeds>(
                    new PluginGamedataSeeds(
                        variables,
                        texts,
                        NullLogger<PluginGamedataSeeds>.Instance
                    )
                )
                .BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);

        var module = registry.Modules.Should().ContainSingle().Which;
        module.Key.Should().Be("hello");
        module.Url.Should().StartWith("/gamedata/plugins/hello/hello.js?v=");
        registry.FindFile("hello", "hello.js").Should().NotBeNull();

        registry
            .FindFile("hello", "hello.png")
            .Should()
            .NotBeNull("the module loads its icon beside it");
        (await texts.GetValuesAsync(["hello.window.title", "hello.window.clicks"], Ct))
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["hello.window.title"] = "Hello",
                    ["hello.window.clicks"] = "Clicks: %count%",
                }
            );
        (await variables.GetValuesAsync(["hello.bar.color", "hello.bar.width"], Ct))
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["hello.bar.color"] = "\"#33cc99\"",
                    ["hello.bar.width"] = "40",
                }
            );

        await manager.UnloadAllAsync(Ct);

        registry.Modules.Should().BeEmpty();
    }

    private static string RepositoryRoot()
    {
        for (
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent
        )
            if (File.Exists(Path.Combine(dir.FullName, "Turbo.Cloud.sln")))
                return dir.FullName;

        throw new InvalidOperationException("Turbo.Cloud.sln was not found above the tests.");
    }
}
