using System.IO;
using FluentAssertions;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Plugins;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// A plugin needs no plugin class: an assembly without one loads, its pieces are found by the
/// scan, its <c>wwwroot/&lt;key&gt;.js</c> is its client module and its
/// <c>[PluginSettings]</c> classes are bound and listed, as a <c>TurboPlugin</c>'s are.
/// </summary>
public sealed class PluginConventionTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-convention-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task An_assembly_with_no_plugin_class_loads_with_its_command_module_and_settings()
    {
        PluginBuilder.Write(_folder, "rates", SOURCE);
        Directory.CreateDirectory(Path.Combine(_folder, "rates", "wwwroot"));
        await File.WriteAllTextAsync(
            Path.Combine(_folder, "rates", "wwwroot", "rates.js"),
            "export default { activate() {} };",
            Ct
        );

        var host = new PluginTestHost(_db, _fakes);
        var manager = host.Manager(_folder);

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);

        _fakes
            .Log.On<ICommandRegistryProvider>()
            .SelectMany(c => (IEnumerable<ICommand>)c.Args[0]!)
            .Select(c => c.GetType().Name)
            .Should()
            .Equal("RateCommand");
        host.Modules.Modules.Should()
            .ContainSingle()
            .Which.Url.Should()
            .StartWith("/gamedata/plugins/rates/rates.js?v=");
        host.SettingsType.Should().NotBeNull().And.Subject!.Name.Should().Be("RateSettings");

        await manager.UnloadAllAsync(Ct);

        host.Modules.Modules.Should().BeEmpty();
    }

    private const string SOURCE = """
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.Options;
        using Turbo.Contracts.Plugins;
        using Turbo.Primitives.Commands;

        [PluginSettings("Rates")]
        public sealed class RateSettings
        {
            public int Rate { get; init; } = 5;
        }

        [Command("rate")]
        public sealed class RateCommand(IOptionsMonitor<RateSettings> settings) : ICommand<RateCommand.Arguments>
        {
            public sealed record Arguments;

            public ValueTask<CommandResult> ExecuteAsync(ICommandContext ctx, Arguments arguments, CancellationToken ct) =>
                ValueTask.FromResult(CommandResult.Done("rate", settings.CurrentValue.Rate.ToString()));
        }
        """;
}
