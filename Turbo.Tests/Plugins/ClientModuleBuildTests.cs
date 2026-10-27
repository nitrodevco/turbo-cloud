using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.ClientModules;
using Turbo.Gamedata.Configuration;
using Turbo.Plugins;
using Turbo.Plugins.ClientBuild;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Exceptions;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Plugins;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// A plugin's client module is built by the server from its TypeScript under <c>client/src/</c>,
/// with the esbuild shipped beside it: its own sources, the SDK and the client's shared libraries,
/// nothing else, so no prebuilt or minified code reaches a client. A module that does not build
/// fails the plugin at the line at fault; an unchanged one is built once.
/// </summary>
public sealed class ClientModuleBuildTests : IDisposable
{
    private const string KEY = "sample";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-client-build-" + Guid.NewGuid().ToString("N")
    );
    private readonly ClientModuleRegistry _registry = new(
        new TestOptionsMonitor<GamedataConfig>(new GamedataConfig()),
        NullLogger<ClientModuleRegistry>.Instance
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    /// <summary>A plugin with code (no plugin class) and the files given, by their paths in its folder.</summary>
    private void WritePlugin(params (string Path, string Text)[] files)
    {
        PluginBuilder.Write(_folder, KEY, "public static class Marker { }");

        foreach (var (path, text) in files)
            WriteFile(path, text);
    }

    private void WriteFile(string path, string text)
    {
        var file = Path.Combine(_folder, KEY, path);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
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

    private string Served() => File.ReadAllText(_registry.FindFile(KEY, $"{KEY}.js")!);

    [Fact]
    public async Task A_plugins_typescript_is_built_into_one_module_on_the_clients_own_react()
    {
        WritePlugin(
            (
                "client/src/index.tsx",
                """
                import { defineModule } from '@nitrodevco/client-module-sdk';
                import { Box } from '@nitrodevco/client-module-sdk/theme';
                import { useState } from 'react';
                import { greeting } from './greeting';

                const Greeting = () => {
                    const [ count ] = useState(1);

                    return <Box>{greeting(count)}</Box>;
                };

                export default defineModule({ activate(api) { api.windows.register('greeting', Greeting); } });
                """
            ),
            (
                "client/src/greeting.ts",
                "export const greeting = (count: number): string => `hello ${count}`;"
            ),
            // What a plugin built before: not its module, not served.
            ($"wwwroot/{KEY}.js", "export default { prebuilt: true };")
        );

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);
        _registry
            .Modules.Should()
            .ContainSingle()
            .Which.Url.Should()
            .StartWith($"/gamedata/plugins/{KEY}/{KEY}.js?v=");
        Served()
            .Should()
            .Contain(
                "var greeting = (count) => `hello ${count}`;",
                "its own file, types taken out, not minified"
            )
            .And.Contain("globalThis.NitroClientModuleHost", "React is the client's, never bundled")
            .And.Contain("//# sourceMappingURL=data:application/json;base64,")
            .And.NotContain("prebuilt");

        await manager.UnloadAllAsync(Ct);

        _registry.Modules.Should().BeEmpty();
    }

    [Theory]
    // A package, even one the plugin's folder holds: no node_modules is bundled.
    [InlineData(
        "import _ from 'lodash'; console.log(_);",
        "client/src/index.ts:2:14: \"lodash\" is not built into a client module"
    )]
    // Prebuilt JavaScript beside the sources.
    [InlineData(
        "import { v } from './vendor.js'; console.log(v);",
        "client/src/index.ts:2:18: \"./vendor.js\" is not a TypeScript source under client/src/"
    )]
    // esbuild's own error, where it puts it.
    [InlineData("const x = ;", "client/src/index.ts:2:10: Unexpected \";\"")]
    public async Task A_module_that_does_not_build_fails_the_plugin_at_the_line_at_fault(
        string line,
        string error
    )
    {
        WritePlugin(
            ("client/src/index.ts", $"// {KEY}\n{line}\nexport default {{}};\n"),
            ("client/src/vendor.js", "export const v = 1;"),
            ("node_modules/lodash/index.js", "module.exports = {};")
        );

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Failed);
        status
            .Error.Should()
            .Contain($"The client module of {KEY} did not build")
            .And.Contain(error);
        _registry.Modules.Should().BeEmpty();
    }

    [Fact]
    public void An_unchanged_module_is_taken_from_the_cache_and_a_changed_one_built_again()
    {
        WritePlugin(("client/src/index.ts", "export default { activate() {} };"));

        var builder = new PluginClientBuilder(
            Path.Combine(_folder, PluginClientBuilder.CACHE_FOLDER)
        );
        var folder = Path.Combine(_folder, KEY);
        var first = builder.Build(KEY, folder, "client/src/index.ts");
        var again = builder.Build(KEY, folder, "client/src/index.ts");

        first.Reused.Should().BeFalse();
        again.Should().Be(first with { Reused = true, Milliseconds = 0 });

        WriteFile(
            "client/src/index.ts",
            "export default { activate() { console.log('changed'); } };"
        );

        var changed = builder.Build(KEY, folder, "client/src/index.ts");

        changed.Reused.Should().BeFalse();
        changed.File.Should().NotBe(first.File);
        File.ReadAllText(changed.File).Should().Contain("changed");
    }

    /// <summary>
    /// A cache entry made on another server (shipped beside a stub source, with whatever JavaScript
    /// in it) is never served: the cache is named under this server's own secret.
    /// </summary>
    [Fact]
    public void A_cache_entry_built_elsewhere_is_not_served()
    {
        WritePlugin(("client/src/index.ts", "export default { activate() {} };"));

        var folder = Path.Combine(_folder, KEY);
        var elsewhere = Path.Combine(_folder, "elsewhere");
        var shipped = new PluginClientBuilder(elsewhere).Build(KEY, folder, "client/src/index.ts");

        File.WriteAllText(shipped.File, "/* not built from this source */");

        var cache = Path.Combine(_folder, PluginClientBuilder.CACHE_FOLDER);
        var target = Path.Combine(cache, KEY, Path.GetFileName(Path.GetDirectoryName(shipped.File)!));

        Directory.CreateDirectory(target);
        File.Copy(shipped.File, Path.Combine(target, Path.GetFileName(shipped.File)));

        var built = new PluginClientBuilder(cache).Build(KEY, folder, "client/src/index.ts");

        built.Reused.Should().BeFalse();
        File.ReadAllText(built.File).Should().NotContain("not built from this source");
    }

    [Fact]
    public async Task Editing_a_client_source_reloads_the_plugin_with_its_module_built_again()
    {
        WritePlugin(("client/src/index.ts", "export default { activate() {} };"));

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);

        var before = _registry.Modules.Single().Sha256;

        // What the hot reload watcher sees change, and has the plugin loaded again.
        PluginHelpers
            .IsCodeFile(Path.Combine(_folder, KEY, "client", "src", "index.ts"))
            .Should()
            .BeTrue();
        WriteFile(
            "client/src/index.ts",
            "export default { activate() { console.log('edited'); } };"
        );
        await manager.LoadChangedAsync(Ct);

        manager.Snapshot().Single().State.Should().Be(PluginStateType.Loaded);
        _registry.Modules.Single().Sha256.Should().NotBe(before);
        Served().Should().Contain("edited");

        await manager.UnloadAllAsync(Ct);
    }

    [Fact]
    public void Without_esbuild_the_build_says_how_to_get_it()
    {
        WritePlugin(("client/src/index.ts", "export default {};"));

        var builder = new PluginClientBuilder(
            Path.Combine(_folder, PluginClientBuilder.CACHE_FOLDER),
            esbuild: Path.Combine(_folder, "none", "esbuild")
        );
        var build = () => builder.Build(KEY, Path.Combine(_folder, KEY), "client/src/index.ts");

        build
            .Should()
            .Throw<PluginClientBuildException>()
            .WithMessage(
                $"*esbuild {PluginClientBuilder.ESBUILD_VERSION} is not at*Build or publish the server again*"
            );
    }
}
