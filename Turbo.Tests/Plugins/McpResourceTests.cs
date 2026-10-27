using System.ComponentModel;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Plugins.Mcp;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Plugins;
using Turbo.Testing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// Plugins' MCP resources and prompts (<c>docs/plugin-mcp-tools.md</c>): a compiled plugin's are
/// listed once it starts and gone when it unloads, a URI template's values are read into its
/// arguments (one that does not fit is no resource), a caller reads and gets only what it holds a
/// node of (to it, the rest are not there), prompt arguments sent as text are checked, one that
/// throws is logged with its plugin, an address two plugins use or a template value with no
/// property fails the load, and a tool's answer carries resource links.
/// </summary>
public sealed class McpResourceTests : IDisposable
{
    private const string DESK = """
        using System;
        using System.ComponentModel;
        using System.Threading;
        using System.Threading.Tasks;
        using Turbo.Contracts.Plugins.Mcp;
        using Turbo.Primitives.Players.Permissions;

        namespace Desk;

        public sealed record RoomArgs(int RoomId);

        public sealed record RoomView(int Id, PlayerText Name);

        [RequiresPermission("plugin.desk.rooms")]
        public sealed class RoomResource : McpResourceTemplate<RoomArgs>
        {
            public override string UriTemplate => "turbo://rooms/{roomId}";
            public override string Name => "room";
            public override string Description => "A room's state.";

            public override Task<McpResourceResult> HandleAsync(RoomArgs args, McpToolContext ctx, CancellationToken ct) =>
                Task.FromResult(args.RoomId == 404
                    ? McpResourceResult.NotFound("There is no room 404.")
                    : McpResourceResult.Data(new RoomView(args.RoomId, "Lobby")));
        }

        [RequiresPermission("plugin.desk.rooms")]
        public sealed class StatusResource : McpResource
        {
            public override string Uri => "turbo://desk/status";
            public override string Name => "desk_status";
            public override string MimeType => "text/plain";
            public override string Description => "Whether the desk is open.";

            public override Task<McpResourceResult> HandleAsync(McpToolContext ctx, CancellationToken ct) =>
                Task.FromResult(McpResourceResult.Contents("open"));
        }

        [RequiresPermission("plugin.desk.rooms")]
        public sealed class BrokenResource : McpResource
        {
            public override string Uri => "turbo://desk/broken";
            public override string Name => "broken";
            public override string Description => "Always fails.";

            public override Task<McpResourceResult> HandleAsync(McpToolContext ctx, CancellationToken ct) =>
                throw new InvalidOperationException("boom");
        }

        public sealed record WiredArgs(
            [Description("The room's id.")] int RoomId,
            [Description("What went wrong, in the staff member's words.")] string? Symptom = null
        );

        [RequiresPermission("plugin.desk.rooms")]
        public sealed class WiredPrompt : McpPrompt<WiredArgs>
        {
            public override string Name => "investigate_wired";
            public override string Description => "Investigate the wired in a room.";

            public override Task<McpPromptResult> HandleAsync(WiredArgs args, McpToolContext ctx, CancellationToken ct) =>
                Task.FromResult(McpPromptResult.Of(
                    $"Wired in room {args.RoomId}",
                    McpPromptMessage.User($"Look into room {args.RoomId}: {args.Symptom ?? "no symptom given"}."),
                    McpPromptMessage.LinkTo(new McpResourceLink($"turbo://rooms/{args.RoomId}", "room"))));
        }
        """;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-mcp-res-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task A_plugins_resources_and_prompts_are_listed_once_it_starts_and_gone_when_it_unloads()
    {
        var host = new PluginTestHost(_db, _fakes);
        var manager = await LoadAsync(host, ("desk", DESK));
        IMcpResources resources = host.Mcp;
        IMcpPrompts prompts = host.Mcp;

        resources
            .All.Select(x => (x.Uri, x.IsTemplate))
            .Should()
            .Equal(
                ("turbo://desk/broken", false),
                ("turbo://desk/status", false),
                ("turbo://rooms/{roomId}", true)
            );
        prompts
            .All.Single()
            .Arguments.Should()
            .Equal(
                new McpPromptArgument("roomId", "The room's id.", true),
                new McpPromptArgument(
                    "symptom",
                    "What went wrong, in the staff member's words.",
                    false
                )
            );
        manager
            .ContributionsOf("desk")!
            .Items.Should()
            .Contain(
                new PluginContribution(
                    PluginContributionKinds.MCP_RESOURCE,
                    "turbo://rooms/{roomId}",
                    "room; plugin.desk.rooms"
                )
            )
            .And.Contain(
                new PluginContribution(
                    PluginContributionKinds.MCP_PROMPT,
                    "investigate_wired",
                    "plugin.desk.rooms"
                )
            );

        await manager.UnloadAllAsync(Ct);

        resources.All.Should().BeEmpty();
        prompts.All.Should().BeEmpty();
    }

    [Fact]
    public async Task A_templates_values_are_read_into_its_arguments_and_one_that_does_not_fit_is_no_resource()
    {
        var host = new PluginTestHost(_db, _fakes);
        await LoadAsync(host, ("desk", DESK));
        var staff = Staff(host, "plugin.desk.rooms");
        IMcpResources resources = host.Mcp;

        var room = await resources.ReadAsync("turbo://rooms/42", new McpCaller(staff, true), Ct);
        var status = await resources.ReadAsync("turbo://desk/status", new McpCaller(staff), Ct);
        var letters = await resources.ReadAsync("turbo://rooms/abc", new McpCaller(staff), Ct);
        var missing = await resources.ReadAsync("turbo://rooms/404", new McpCaller(staff), Ct);

        room.Status.Should().Be(McpResourceReadStatus.Ok, "reading changes nothing");
        room.Contents.Should()
            .Equal(
                new McpResourceContents(
                    "turbo://rooms/42",
                    "application/json",
                    """{"id":42,"name":{"playerWritten":"Lobby"}}"""
                )
            );
        room.Note.Should().Be(PlayerText.NOTE);
        status
            .Contents.Single()
            .Should()
            .Be(new McpResourceContents("turbo://desk/status", "text/plain", "open"));
        status.Note.Should().BeNull();
        letters.Status.Should().Be(McpResourceReadStatus.NotFound);
        letters.Message.Should().Contain("\"roomId\" must be a whole number; it was \"abc\"");
        missing.Status.Should().Be(McpResourceReadStatus.NotFound);
        missing.Message.Should().Contain("There is no room 404.");
    }

    [Fact]
    public async Task A_caller_without_a_node_is_told_a_resource_or_prompt_is_not_there_as_for_none()
    {
        var host = new PluginTestHost(_db, _fakes);
        await LoadAsync(host, ("desk", DESK));
        var player = new McpCaller(Staff(host));
        IMcpResources resources = host.Mcp;
        IMcpPrompts prompts = host.Mcp;

        (await resources.AvailableToAsync(player, Ct)).Should().BeEmpty();
        (await prompts.AvailableToAsync(player, Ct)).Should().BeEmpty();
        (await resources.ReadAsync("turbo://rooms/1", player, Ct))
            .Should()
            .Be(
                McpResourceReadResult.NotFound("turbo://rooms/1"),
                "what an address with no resource answers"
            );
        (await prompts.GetAsync("investigate_wired", null, player, Ct))
            .Should()
            .Be(McpPromptGetResult.Unavailable("investigate_wired"));
    }

    [Fact]
    public async Task A_prompts_arguments_sent_as_text_are_read_and_checked()
    {
        var host = new PluginTestHost(_db, _fakes);
        await LoadAsync(host, ("desk", DESK));
        var staff = new McpCaller(Staff(host, "plugin.desk.rooms"), true);
        IMcpPrompts prompts = host.Mcp;

        var got = await prompts.GetAsync(
            "investigate_wired",
            new Dictionary<string, string> { ["roomId"] = "42" },
            staff,
            Ct
        );
        var none = await prompts.GetAsync("investigate_wired", null, staff, Ct);
        var wrong = await prompts.GetAsync(
            "investigate_wired",
            new Dictionary<string, string> { ["roomId"] = "lobby", ["room"] = "1" },
            staff,
            Ct
        );

        got.Status.Should().Be(McpPromptGetStatus.Ok);
        got.Description.Should().Be("Wired in room 42");
        got.Messages.Should()
            .Equal(
                McpPromptMessage.User("Look into room 42: no symptom given."),
                McpPromptMessage.LinkTo(new McpResourceLink("turbo://rooms/42", "room"))
            );
        none.Status.Should().Be(McpPromptGetStatus.InvalidArguments);
        none.Message.Should().Contain("\"roomId\" is required: The room's id.");
        wrong
            .Message.Should()
            .Contain("There is no argument \"room\"; the arguments are \"roomId\", \"symptom\".")
            .And.Contain("\"roomId\" must be a whole number; it was \"lobby\".");
    }

    [Fact]
    public async Task A_resource_that_throws_answers_failed_without_its_error_and_is_logged_with_its_plugin()
    {
        var host = new PluginTestHost(_db, _fakes);
        await LoadAsync(host, ("desk", DESK));
        IMcpResources resources = host.Mcp;

        var read = await resources.ReadAsync(
            "turbo://desk/broken",
            new McpCaller(Staff(host, "plugin.desk.rooms")),
            Ct
        );

        read.Status.Should().Be(McpResourceReadStatus.Failed);
        read.Message.Should().NotContain("boom");
        host.McpLog.AtLeast(LogLevel.Error)
            .Should()
            .ContainSingle(x => x.Message.Contains("desk") && x.Message.Contains("broken"));
    }

    [Fact]
    public async Task Two_plugins_cannot_offer_a_resource_at_one_address()
    {
        var host = new PluginTestHost(_db, _fakes);
        var manager = await LoadAsync(
            host,
            ("desk", DESK),
            ("copy", DESK.Replace("namespace Desk;", "namespace Copy;"))
        );

        var failed = manager.Snapshot().Single(x => x.State == PluginStateType.Failed);

        failed.Error.Should().Contain("has the address of the plugin");
    }

    [Fact]
    public async Task A_template_value_its_arguments_have_no_property_for_fails_the_load()
    {
        var host = new PluginTestHost(_db, _fakes);
        var manager = await LoadAsync(
            host,
            ("desk", DESK.Replace("turbo://rooms/{roomId}", "turbo://rooms/{room}"))
        );

        manager
            .Snapshot()
            .Single()
            .Error.Should()
            .Contain(
                "The MCP resource room's address has {room}, which its arguments type RoomArgs has no property for."
            );
    }

    [Fact]
    public async Task A_tools_answer_carries_its_resource_links()
    {
        var call = await new McpToolTester(new LinkTool())
            .As(new PlayerId(1), "*")
            .CallAsync("open_room");

        call.AssertOk()
            .Links.Should()
            .Equal(new McpResourceLink("turbo://rooms/7", "room", MimeType: "application/json"));
        call.Text.Should().Be("Room 7 is open.");
    }

    [RequiresPermission("test.rooms")]
    private sealed class LinkTool : McpTool<McpNoInput>
    {
        public override string Name => "open_room";
        public override string Description => "Opens a room.";

        public override Task<McpToolResult> HandleAsync(
            McpNoInput input,
            McpToolContext ctx,
            CancellationToken ct
        ) =>
            Task.FromResult(
                McpToolResult
                    .Text("Room 7 is open.")
                    .WithLinks(
                        new McpResourceLink("turbo://rooms/7", "room", MimeType: "application/json")
                    )
            );
    }

    private static PlayerId Staff(PluginTestHost host, params string[] nodes)
    {
        var staff = host.Hotel.Players.Add("Staff");

        staff.Permissions.UnionWith(nodes);

        return staff.Id;
    }

    private async Task<Turbo.Plugins.PluginManager> LoadAsync(
        PluginTestHost host,
        params (string Key, string Source)[] plugins
    )
    {
        foreach (var (key, source) in plugins)
            PluginBuilder.Write(_folder, key, source);

        var manager = host.Manager(_folder);

        await manager.LoadAllAsync(ct: Ct);

        return manager;
    }
}
