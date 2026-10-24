using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Gamedata.Api;
using Turbo.Gamedata.ClientModules;
using Turbo.Gamedata.Configuration;
using Turbo.Messages.Registry;
using Turbo.Networking.Extensions;
using Turbo.PacketHandlers.Turbo;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Availability;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// <c>client.modules</c>: a plugin declares a JavaScript module in its folder's <c>wwwroot/</c>,
/// the server works out its address and SHA-256 and tells every client that asked, again when the
/// plugin goes, and the gamedata host serves the files, cached for good by their hash. A client
/// that never asks is told nothing.
/// </summary>
public sealed class ClientModuleTests : IAsyncDisposable
{
    private const int PLAYER = 10;
    private const string SCRIPT = "export default { activate() {}, deactivate() {} };\n";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-client-modules-" + Guid.NewGuid().ToString("N")
    );
    private readonly Fakes _fakes = new();
    private readonly TestOptionsMonitor<GamedataConfig> _config = new(new GamedataConfig());
    private readonly ClientModuleRegistry _registry;
    private GamedataServer? _server;

    public ClientModuleTests()
    {
        Directory.CreateDirectory(_folder);

        _registry = new ClientModuleRegistry(_config, NullLogger<ClientModuleRegistry>.Instance);
        _fakes.Handlers["GetOnlinePlayerIds"] = _ =>
            (IReadOnlyCollection<PlayerId>)new List<PlayerId> { PLAYER };
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.StopAsync(CancellationToken.None);
            await _server.DisposeAsync();
        }

        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string Sha(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>The hello plugin on disk: it registers its module and never takes it away itself.</summary>
    private void WriteHelloPlugin()
    {
        PluginBuilder.Write(_folder, "hello", HELLO);

        var wwwroot = Path.Combine(_folder, "hello", "wwwroot");

        Directory.CreateDirectory(wwwroot);
        File.WriteAllText(Path.Combine(wwwroot, "hello.js"), SCRIPT);
    }

    private PluginManager Manager() =>
        new(
            new ServiceCollection()
                .AddSingleton<IClientModuleDirectory>(_registry)
                .BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

    private List<TurboClientModulesMessage> Sent() =>
        [.. _fakes.SentToPlayer<TurboClientModulesMessage>(PLAYER)];

    [Fact]
    public async Task A_plugins_module_is_sent_to_everyone_online_with_its_hash_and_taken_away_when_it_unloads()
    {
        WriteHelloPlugin();

        var broadcast = new ClientModuleBroadcast(
            _registry,
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            NullLogger<ClientModuleBroadcast>.Instance
        );
        await broadcast.StartAsync(Ct);

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);

        var hash = Sha(SCRIPT);
        var module = Sent().Should().ContainSingle().Which.Modules.Should().ContainSingle().Which;

        module
            .Should()
            .Be(
                new ClientModuleSnapshot
                {
                    Key = "hello",
                    Url = $"/gamedata/plugins/hello/hello.js?v={hash[..16]}",
                    Version = "1.0.0",
                    Sha256 = hash,
                }
            );

        await manager.UnloadAllAsync(Ct);

        _registry.Modules.Should().BeEmpty("unloading the plugin takes its module away");
        Sent().Should().HaveCount(2).And.Subject.Last().Modules.Should().BeEmpty();

        await broadcast.StopAsync(Ct);
    }

    [Fact]
    public async Task A_plugin_without_a_module_has_its_wwwroot_served_while_it_is_loaded()
    {
        // A theme: files its variables name, no module.
        PluginBuilder.Write(_folder, "theme", THEME);

        var wwwroot = Path.Combine(_folder, "theme", "wwwroot");

        Directory.CreateDirectory(wwwroot);
        File.WriteAllText(Path.Combine(wwwroot, "habbo-room-ui-com.nitro"), "library");

        var manager = Manager();
        var http = await StartServerAsync();

        await manager.LoadAllAsync(ct: Ct);

        manager.Snapshot().Single().State.Should().Be(PluginStateType.Loaded);
        _registry.Modules.Should().BeEmpty("clients are sent no module for it");

        using (
            var file = await http.GetAsync("/gamedata/plugins/theme/habbo-room-ui-com.nitro", Ct)
        )
        {
            file.StatusCode.Should().Be(HttpStatusCode.OK);
            (await file.Content.ReadAsStringAsync(Ct)).Should().Be("library");
        }

        await manager.UnloadAllAsync(Ct);

        using var gone = await http.GetAsync("/gamedata/plugins/theme/habbo-room-ui-com.nitro", Ct);

        gone.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void The_address_starts_with_the_hotels_public_gamedata_address_when_it_has_one()
    {
        _config.Set(new GamedataConfig { PublicUrl = "https://gamedata.example.com/" });
        using var _ = Add("hello", "sub/hello world.js");

        _registry
            .Modules.Single()
            .Url.Should()
            .Be(
                $"https://gamedata.example.com/gamedata/plugins/hello/sub/hello%20world.js?v={Sha(SCRIPT)[..16]}"
            );
    }

    [Fact]
    public void A_module_is_refused_outside_its_folder_under_another_plugins_key_or_without_a_plugin()
    {
        WriteHelloPlugin();

        var root = Path.Combine(_folder, "hello", "wwwroot");
        var manifest = new PluginManifest
        {
            Name = "hello",
            Key = "hello",
            Version = "1.0.0",
            Author = "Staff",
            AssemblyFile = "hello.dll",
        };
        using var plugin = new PluginClientModules(
            _registry,
            manifest,
            Path.Combine(_folder, "hello")
        );

        var outside = () => plugin.Register("hello", "../manifest.json");
        var missing = () => plugin.Register("hello", "nope.js");
        var other = () => plugin.Register("other", "hello.js");
        var core = () => _registry.Register("hello", "hello.js");

        outside.Should().Throw<ArgumentException>();
        missing.Should().Throw<ArgumentException>();
        other.Should().Throw<ArgumentException>().WithMessage("*own key, hello*");
        core.Should().Throw<InvalidOperationException>();

        using var registration = plugin.Register("hello", "hello.js");
        var twice = () => plugin.Register("hello", "hello.js");

        twice.Should().Throw<InvalidOperationException>();
        File.WriteAllText(Path.Combine(root, ".env"), "secret");
        _registry.FindFile("hello", ".env").Should().BeNull();
        _registry.FindFile("hello", "../manifest.json").Should().BeNull();
        _registry.FindFile("hello", "..%2Fmanifest.json").Should().BeNull();
        _registry.FindFile("other", "hello.js").Should().BeNull();
        _registry.FindFile("hello", "hello.js").Should().Be(Path.Combine(root, "hello.js"));
    }

    [Fact]
    public async Task The_gamedata_host_serves_a_modules_files_to_any_site_cached_for_good_by_hash()
    {
        using var registration = Add("hello", "hello.js");
        await File.WriteAllTextAsync(
            Path.Combine(_folder, "hello", "wwwroot", "chunk.js"),
            "export {};",
            Ct
        );
        var http = await StartServerAsync();
        var url = _registry.Modules.Single().Url;

        using var entry = await http.GetAsync(url, Ct);

        entry.StatusCode.Should().Be(HttpStatusCode.OK);
        entry.Content.Headers.ContentType!.MediaType.Should().Be("text/javascript");
        entry.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("*");
        entry.Headers.CacheControl!.ToString().Should().Contain("immutable");
        (await entry.Content.ReadAsStringAsync(Ct)).Should().Be(SCRIPT);

        // What the module imports beside it is asked for again each time, answered 304 while the same.
        using var chunk = await http.GetAsync("/gamedata/plugins/hello/chunk.js", Ct);

        chunk.StatusCode.Should().Be(HttpStatusCode.OK);
        chunk.Headers.CacheControl!.NoCache.Should().BeTrue();

        using var again = new HttpRequestMessage(
            HttpMethod.Get,
            "/gamedata/plugins/hello/chunk.js"
        );
        again.Headers.IfNoneMatch.Add(chunk.Headers.ETag!);
        using var notModified = await http.SendAsync(again, Ct);

        notModified.StatusCode.Should().Be(HttpStatusCode.NotModified);

        using var stale = await http.GetAsync("/gamedata/plugins/hello/hello.js?v=0000", Ct);

        stale
            .Headers.CacheControl!.NoCache.Should()
            .BeTrue("an old version's address is not cached for good");

        foreach (
            var path in new[]
            {
                "hello/..%2F..%2Fhello%2Fmanifest.json",
                "hello/missing.js",
                "other/hello.js",
            }
        )
        {
            using var none = await http.GetAsync("/gamedata/plugins/" + path, Ct);

            none.StatusCode.Should().Be(HttpStatusCode.NotFound, path);
        }

        using var post = await http.PostAsync(url, new StringContent(""), Ct);

        post.IsSuccessStatusCode.Should().BeFalse("the files are only read");
    }

    [Fact]
    public async Task A_client_that_asks_for_client_modules_is_sent_them_and_one_that_does_not_is_sent_nothing()
    {
        using var registration = Add("hello", "hello.js");

        await CapabilitiesAsync(ClientCapabilities.LIVE_NOTICES);

        Sent().Should().BeEmpty();

        await CapabilitiesAsync(ClientCapabilities.CLIENT_MODULES);

        Sent().Should().ContainSingle().Which.Modules.Select(x => x.Key).Should().Equal("hello");
        // The presence drops it for a session that did not accept the extension, a later change too.
        new TurboClientModulesMessage { Modules = [] }
            .Capability.Should()
            .Be(ClientCapabilities.CLIENT_MODULES);
        ClientCapabilities.SUPPORTED.Should().Contain(ClientCapabilities.CLIENT_MODULES, 1);
    }

    [Fact]
    public void The_list_is_written_as_the_client_reads_it()
    {
        var packet = PacketHarness.Encode(
            new TurboClientModulesMessage
            {
                Modules =
                [
                    new ClientModuleSnapshot
                    {
                        Key = "hello",
                        Url = "/gamedata/plugins/hello/hello.js?v=abc",
                        Version = "1.0.0",
                        Sha256 = "abc",
                    },
                ],
            }
        );

        packet.Header.Should().Be(30009);
        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be("hello");
        packet.PopString().Should().Be("/gamedata/plugins/hello/hello.js?v=abc");
        packet.PopString().Should().Be("1.0.0");
        packet.PopString().Should().Be("abc");
        packet.End.Should().BeTrue();
    }

    private IDisposable Add(string key, string entry)
    {
        var root = Path.Combine(_folder, key, "wwwroot");
        var file = Path.Combine(root, entry);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, SCRIPT);

        return _registry.Add(new ClientModuleSource(key, "1.0.0", root, entry));
    }

    private async Task<HttpClient> StartServerAsync()
    {
        var port = TestPorts.Free();

        _server = new GamedataServer(
            new ServiceCollection()
                .AddSingleton(_fakes.Create<IGamedataFileService>())
                .AddSingleton<IClientModuleDirectory>(_registry)
                .BuildServiceProvider(),
            Options.Create(new GamedataConfig { Enabled = true, Url = $"http://127.0.0.1:{port}" }),
            NullLoggerFactory.Instance,
            NullLogger<GamedataServer>.Instance
        );

        await _server.StartAsync(Ct);

        return new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
    }

    private async Task CapabilitiesAsync(string capability)
    {
        var handler = new TurboClientCapabilitiesMessageHandler(
            _fakes.Create<Orleans.IGrainFactory>(),
            new ExtensionPacketRegistry(),
            _fakes.Create<ICommandTreeService>(),
            _fakes.Create<IHotelCountdownNotice>(),
            _registry,
            _fakes.Create<Turbo.Primitives.Plugins.Messages.IPluginMessageDirectory>()
        );

        await handler.HandleAsync(
            new TurboClientCapabilitiesMessage
            {
                Capabilities = [new ClientCapabilitySnapshot { Name = capability, Version = 1 }],
            },
            new MessageContext(_fakes.Create<ISessionContext>("session"), PLAYER, -1),
            Ct
        );
    }

    private const string HELLO = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;
        using Turbo.Primitives.ClientModules;

        public sealed class HelloPlugin : ITurboPlugin
        {
            public string Key => "hello";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }

            public Task StartAsync(IServiceProvider services, CancellationToken ct)
            {
                services.GetRequiredService<IHostServices>()
                    .GetRequiredService<IClientModuleRegistry>()
                    .Register("hello", "hello.js");

                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
        """;

    private const string THEME = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;

        public sealed class ThemePlugin : ITurboPlugin
        {
            public string Key => "theme";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }
            public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
        """;
}
