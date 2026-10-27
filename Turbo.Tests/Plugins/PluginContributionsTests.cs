using System.Collections.Immutable;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Turbo.Plugins;
using Turbo.Primitives.Plugins;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The extension inspector (docs/extensions.md roadmap step 5): what a plugin contributes is
/// listed by kind from what its load registered, a failed load shows its error beside what it had
/// registered, and what clients report of its client half (a refused module, a patch that matched
/// nothing) shows on it, and is logged on the server naming the setting to change.
/// </summary>
public sealed class PluginContributionsTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-contributions-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task A_plugin_lists_each_kind_it_contributes()
    {
        // Core already has a logic of the plugin's furni logic key: the plugin's replaces it.
        _fakes.Handlers["get_LogicTypes"] = _ => (IReadOnlyList<string>)["default_floor"];
        PluginBuilder.Write(_folder, "inspect", EVERYTHING);
        var wwwroot = Path.Combine(_folder, "inspect", "wwwroot");
        var client = Path.Combine(_folder, "inspect", "client", "src");
        Directory.CreateDirectory(wwwroot);
        Directory.CreateDirectory(client);
        await File.WriteAllTextAsync(
            Path.Combine(client, "index.ts"),
            "export default { activate() {} };",
            Ct
        );
        await File.WriteAllTextAsync(
            Path.Combine(wwwroot, "template-patches.json"),
            """{ "patches": [ { "template": "habbo-toolbar-com/purse_xml", "ops": [ { "element": "credits", "set": { "hidden": true } }, { "element": "help", "remove": true } ] } ] }""",
            Ct
        );

        var manager = new PluginTestHost(_db, _fakes).Manager(_folder);

        await manager.LoadAllAsync(ct: Ct);

        var inspected = manager.ContributionsOf("inspect")!;

        inspected.State.Should().Be(PluginStateType.Loaded, inspected.Error);
        inspected
            .Items.Select(x => (x.Kind, x.Name))
            .Should()
            .Contain([
                (PluginContributionKinds.COMMAND, "inspectme"),
                (PluginContributionKinds.FURNI_LOGIC, "default_floor"),
                (PluginContributionKinds.OVERRIDE, "default_floor"),
                (PluginContributionKinds.MESSAGE, "inspect.ping"),
                (PluginContributionKinds.MESSAGE_HANDLER, "PingHandler"),
                (PluginContributionKinds.PERMISSION_SOURCE, "InspectNodes"),
                (PluginContributionKinds.PERMISSION_NODE, "inspect.use"),
                (PluginContributionKinds.SETTING, "Inspect"),
                (PluginContributionKinds.WEB_ROUTE, "GET values"),
                (PluginContributionKinds.CLIENT_MODULE, "client/src/index.ts"),
                (PluginContributionKinds.TEMPLATE_PATCH, "habbo-toolbar-com/purse_xml"),
                (PluginContributionKinds.HOSTED_SERVICE, "Ticker"),
                (PluginContributionKinds.SCHEDULED_TASK, "inspect.Rotation"),
            ]);
        inspected
            .Items.Single(x => x.Kind == PluginContributionKinds.TEMPLATE_PATCH)
            .Detail.Should()
            .Be("2 op(s)");
        inspected.Conflicts.Should().BeEmpty();

        await manager.UnloadAllAsync(Ct);
    }

    [Fact]
    public async Task A_failed_load_shows_its_error_and_what_it_had_registered()
    {
        PluginBuilder.Write(_folder, "broken", BROKEN);

        var manager = new PluginTestHost(_db, _fakes).Manager(_folder);

        await manager.LoadAllAsync(ct: Ct);

        var inspected = manager.ContributionsOf("broken")!;

        inspected.State.Should().Be(PluginStateType.Failed);
        inspected.Error.Should().Contain("IMissing");
        inspected
            .Items.Select(x => (x.Kind, x.Name))
            .Should()
            .Contain((PluginContributionKinds.SETTING, "Broken"));
        manager.ContributionsOf("nothere").Should().BeNull();
    }

    [Fact]
    public async Task Client_reports_show_on_the_plugin_and_a_refused_module_is_logged_naming_the_setting()
    {
        PluginBuilder.Write(_folder, "inspect", EVERYTHING);

        var host = new PluginTestHost(_db, _fakes);
        var log = host.ReportsLog;
        var packets = new PacketHarness();

        packets.Resolver.Overrides[typeof(IClientExtensionReports)] = host.Reports;

        var manager = host.Manager(_folder);

        await manager.LoadAllAsync(ct: Ct);

        // Two players' clients send the same report, through the extension's packet.
        foreach (var player in new[] { 1, 2 })
            await packets.SendAsync(
                PacketHarness.Incoming("TurboClientExtensionReportMessageEvent"),
                PacketHarness.Payload(w =>
                    w.Int(2)
                        .String("inspect")
                        .String(ClientExtensionReportKinds.MODULE_REFUSED)
                        .String("http://localhost:18441/gamedata/plugins/inspect/inspect.js")
                        .String(ClientExtensionReportKinds.HOST_NOT_ALLOWED)
                        .String("http://localhost:18441 is not in client.modules.hosts.")
                        .String("inspect")
                        .String(ClientExtensionReportKinds.PATCH_UNMATCHED)
                        .String("habbo-toolbar-com/purse_xml")
                        .String("credits_old")
                        .String(
                            "inspect's patch names 'credits_old', which this template does not have."
                        )
                ),
                playerId: player
            );

        var inspected = manager.ContributionsOf("inspect")!;

        inspected
            .ClientIssues.Select(x => (x.Kind, x.Subject, x.Reason, x.Clients))
            .Should()
            .BeEquivalentTo([
                (
                    ClientExtensionReportKinds.MODULE_REFUSED,
                    "http://localhost:18441/gamedata/plugins/inspect/inspect.js",
                    ClientExtensionReportKinds.HOST_NOT_ALLOWED,
                    2
                ),
                // The element it is about, so the panel puts it beside the patch naming credits_old.
                (
                    ClientExtensionReportKinds.PATCH_UNMATCHED,
                    "habbo-toolbar-com/purse_xml",
                    "credits_old",
                    2
                ),
            ]);

        // Logged once for the two clients, naming the external variable to change.
        log.AtLeast(LogLevel.Warning)
            .Select(x => x.Message)
            .Where(x => x.Contains("client.modules.hosts", StringComparison.Ordinal))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Contain("inspect");
        log.AtLeast(LogLevel.Warning)
            .Should()
            .Contain(x => x.Message.Contains("template-patches.json", StringComparison.Ordinal));

        await manager.UnloadAllAsync(Ct);
    }

    /// <summary>
    /// S2 (B/framework/DX-PLAN-20261026.md 3.2, stuck point 1): a root caption patch on the catalogue,
    /// whose code sets its caption, was reported applied and did nothing. The client reports it
    /// <c>patch.ineffective</c>, saying what to do instead; the server keeps it on the plugin and logs it.
    /// </summary>
    [Fact]
    public void An_ineffective_patch_op_shows_on_the_plugin_and_is_logged()
    {
        var log = new CapturingLogger<ClientExtensionReports>();
        var reports = new ClientExtensionReports(log, TimeProvider.System);

        reports.Report(
            1,
            [
                new ClientExtensionReport(
                    "theme",
                    ClientExtensionReportKinds.PATCH_INEFFECTIVE,
                    "habbo-catalog-com/catalog_ubuntu_with_tabs",
                    ".caption",
                    "theme's patch sets caption of '', which changes nothing: the window's code sets caption of that element, and that is what it draws. Bind it with override: true instead."
                ),
            ]
        );

        reports
            .IssuesOf("theme")
            .Select(x => (x.Kind, x.Subject))
            .Should()
            .BeEquivalentTo([
                (
                    ClientExtensionReportKinds.PATCH_INEFFECTIVE,
                    "habbo-catalog-com/catalog_ubuntu_with_tabs"
                ),
            ]);
        log.AtLeast(LogLevel.Warning)
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain("override: true");
    }

    /// <summary>
    /// The client's report of a bind it skips for a placeholder inside another (S3's tab icons) is
    /// 311 characters; cut at 300 it lost the fix it ends with. A message is still bounded.
    /// </summary>
    [Fact]
    public void A_skipped_bind_s_report_keeps_the_fix_it_ends_with()
    {
        const string SKIPPED =
            "habbo-catalog-com/catalog_ubuntu_with_tabs: theme's bind of 'tab_icon' is skipped, so it changes nothing: bind's asset puts a placeholder inside another (${text.tabicon.${item.index}}): placeholders are filled once and do not nest, so it names no key. Bind each row's own value (${item.<key>}) or one whole key.";
        var log = new CapturingLogger<ClientExtensionReports>();
        var reports = new ClientExtensionReports(log, TimeProvider.System);

        reports.Report(
            1,
            [
                new ClientExtensionReport(
                    "theme",
                    ClientExtensionReportKinds.PATCH_INEFFECTIVE,
                    "habbo-catalog-com/catalog_ubuntu_with_tabs",
                    "tab_icon:bind",
                    SKIPPED
                ),
                new ClientExtensionReport(
                    "theme",
                    ClientExtensionReportKinds.PATCH_INEFFECTIVE,
                    "habbo-catalog-com/catalog_ubuntu_with_tabs",
                    "tab_text:bind",
                    new string('x', 5000)
                ),
            ]
        );

        var issues = reports.IssuesOf("theme").ToDictionary(x => x.Message.Length > 900);

        issues[false].Message.Should().Be(SKIPPED);
        issues[true].Message.Length.Should().Be(1000);
        log.AtLeast(LogLevel.Warning).Should().Contain(x => x.Message.EndsWith("one whole key."));
    }

    [Fact]
    public void Reports_of_unknown_kinds_or_malformed_owners_are_dropped()
    {
        var reports = new ClientExtensionReports(
            new CapturingLogger<ClientExtensionReports>(),
            TimeProvider.System
        );

        reports.Report(
            1,
            [
                new ClientExtensionReport("inspect", "anything", "x", "", ""),
                new ClientExtensionReport(
                    "../etc",
                    ClientExtensionReportKinds.MODULE_FAILED,
                    "x",
                    "",
                    ""
                ),
            ]
        );

        reports.IssuesOf("inspect").Should().BeEmpty();
        reports.IssuesOf("../etc").Should().BeEmpty();
    }

    private const string EVERYTHING = """
        using Turbo.Contracts.Furni;
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Hosting;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Scheduling;
        using Turbo.Contracts.Plugins.Web;
        using Turbo.Primitives.Commands;
        using Turbo.Primitives.Furniture.Providers;
        using Turbo.Primitives.Players.Permissions;
        using Turbo.Primitives.Plugins.Messages;
        using Turbo.Primitives.Rooms.Object.Furniture.Floor;
        using Turbo.Primitives.Rooms.Object.Logic;

        public sealed class InspectPlugin : TurboPlugin
        {
            protected override void Services(IServiceCollection services) =>
                services.AddHostedService<Ticker>();
        }

        public sealed class Ticker : IHostedService
        {
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
        }

        [Schedule("0 18 * * MON")]
        public sealed class Rotation : IScheduledTask
        {
            public Task RunAsync(ScheduledTaskRun run, CancellationToken ct) => Task.CompletedTask;
        }

        [PluginSettings("Inspect")]
        public sealed class InspectSettings
        {
            public int Rate { get; init; } = 5;
        }

        [Command("inspectme")]
        public sealed class InspectCommand : ICommand<NoArguments>
        {
            public ValueTask<CommandResult> ExecuteAsync(ICommandContext ctx, NoArguments arguments, CancellationToken ct) =>
                ValueTask.FromResult(CommandResult.Ok);
        }

        public sealed class InspectNodes : IPermissionNodeSource
        {
            public string? Prefix => "inspect";
            public IEnumerable<PermissionNodeDefinition> Nodes => [new("inspect.use", "Use it")];
            public IEnumerable<PermissionMetaDefinition> MetaKeys => [];
        }

        [FurniLogic("default_floor")]
        public sealed class GlowFloorLogic : FurniLogic;

        [PluginMessage("inspect.ping")]
        public sealed record Ping(int Id) : IPluginMessage;

        public sealed class PingHandler : IPluginMessageHandler<Ping>
        {
            public ValueTask HandleAsync(Ping message, PluginMessageContext ctx, CancellationToken ct) =>
                ValueTask.CompletedTask;
        }

        public sealed class ValuesRoute : IPluginWebRoute
        {
            public string Method => "GET";
            public string Pattern => "values";
            public bool Anonymous => true;
            public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct) =>
                Task.FromResult(PluginWebResult.NoContent());
        }
        """;

    private const string BROKEN = """
        using System.Threading;
        using System.Threading.Tasks;
        using Turbo.Contracts.Plugins;
        using Turbo.Primitives.Commands;

        [PluginSettings("Broken")]
        public sealed class BrokenSettings
        {
            public int Rate { get; init; } = 5;
        }

        public interface IMissing { }

        [Command("needsmissing")]
        public sealed class NeedsMissingCommand(IMissing missing) : ICommand<NoArguments>
        {
            public ValueTask<CommandResult> ExecuteAsync(ICommandContext ctx, NoArguments arguments, CancellationToken ct) =>
                ValueTask.FromResult(CommandResult.Ok);
        }
        """;
}
