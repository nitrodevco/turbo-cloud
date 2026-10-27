using System.IO;
using System.Text.Json;
using FluentAssertions;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Testing;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The plugin test kit (Turbo.Testing's <see cref="TestHotel"/>, docs/plugin-testing.md) against a
/// plugin on disk, loaded from its sources as the server loads it: what each of its pieces does
/// when a test plays the game against it is what the server's registries make of it.
/// </summary>
public sealed class TestHotelTests : IAsyncLifetime
{
    private const string MANIFEST = """
        {
          "Name": "Garden",
          "Key": "garden",
          "Version": "1.0.0",
          "Author": "Staff",
          "ApiVersion": 1,
          "TablePrefix": "garden_"
        }
        """;

    private const string SOURCE = """
        using System;
        using System.Collections.Generic;
        using System.ComponentModel.DataAnnotations.Schema;
        using System.Linq;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.EntityFrameworkCore;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Options;
        using Turbo.Contracts.Furni;
        using Turbo.Contracts.Hotel;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Scheduling;
        using Turbo.Contracts.Plugins.Web;
        using Turbo.Contracts.Rooms;
        using Turbo.Database.Context;
        using Turbo.Primitives.Catalog.Enums;
        using Turbo.Primitives.Commands;
        using Turbo.Primitives.Players.Permissions;
        using Turbo.Primitives.Plugins.Messages;
        using Turbo.Primitives.Policies;

        namespace Garden;

        [PluginSettings("Garden")]
        public sealed class GardenSettings
        {
            public int Seeds { get; set; } = 3;
        }

        public sealed class GardenDb : PluginDbContextBase<GardenDb>
        {
            public DbSet<Plant> Plants => Set<Plant>();
        }

        [Table("plants")]
        public sealed class Plant
        {
            public int Id { get; set; }

            public int PlayerId { get; set; }
        }

        [Command("plant", Description = "Plant a seed")]
        [RequiresPermission("plugin.garden.plant")]
        public sealed class PlantCommand(IServiceScopeFactory scopes, IOptionsMonitor<GardenSettings> settings)
            : ICommand<PlantCommand.Arguments>
        {
            public sealed record Arguments;

            public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
                new Dictionary<string, string> { ["planted"] = "Planted %0% of %1%." };

            public async ValueTask<CommandResult> ExecuteAsync(ICommandContext ctx, Arguments arguments, CancellationToken ct)
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<GardenDb>();

                db.Plants.Add(new Plant { PlayerId = ctx.Executor.PlayerId.Value });
                await db.SaveChangesAsync(ct);

                return CommandResult.Done(
                    "planted",
                    (await db.Plants.CountAsync(ct)).ToString(),
                    settings.CurrentValue.Seeds.ToString()
                );
            }
        }

        [Command("rain", Description = "Rain on every garden")]
        [RequiresPermission("plugin.garden.rain")]
        public sealed class RainCommand(IHotel hotel) : IOperatorCommand<RainCommand.Arguments>
        {
            public sealed record Arguments;

            public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
                new Dictionary<string, string>
                {
                    ["reaches"] = "That rains on %0% gardens.",
                    ["rained"] = "It rained on %0% gardens.",
                };

            public async ValueTask<CommandResult> ExecuteAsync(IOperatorCommandContext ctx, Arguments arguments, CancellationToken ct)
            {
                var gardens = ctx.SnapshotRecipients("rain", hotel.Players.OnlineIds());

                if (ctx.ShouldConfirm(gardens.Count))
                    return CommandResult.Confirm("reaches", gardens.Count.ToString());

                foreach (var player in gardens)
                    await ctx.NotifyAsync(player, "garden.rain", "Rain falls on your garden.", [], ct);

                return CommandResult.Done("rained", gardens.Count.ToString());
            }
        }

        [Command("drench", Description = "Drench a player's garden")]
        [RequiresPermission("plugin.garden.rain")]
        public sealed class DrenchCommand : IOperatorCommand<DrenchCommand.Arguments>
        {
            public sealed record Arguments(PlayerTarget Gardener);

            public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
                new Dictionary<string, string> { ["drenched"] = "Drenched the garden of %0%." };

            public async ValueTask<CommandResult> ExecuteAsync(IOperatorCommandContext ctx, Arguments arguments, CancellationToken ct) =>
                await ctx.ResolveAsync(arguments.Gardener, ct) is { Players: [var gardener] }
                    ? CommandResult.Done("drenched", gardener.Name)
                    : CommandResult.Fail(CommandReplyKeys.PLAYER_NOT_FOUND, arguments.Gardener.Text);
        }

        [PluginMessage("garden.water")]
        public sealed record WaterMessage(int Litres) : IPluginMessage;

        [PluginMessage("garden.watered")]
        public sealed record WateredMessage(int Litres, string By) : IPluginMessage;

        public sealed class WaterHandler(IHotel hotel) : IPluginMessageHandler<WaterMessage>
        {
            public async ValueTask HandleAsync(WaterMessage message, PluginMessageContext ctx, CancellationToken ct)
            {
                var player = await hotel.Players.GetAsync(ctx.PlayerId, ct);

                await ctx.ReplyAsync(new WateredMessage(message.Litres * 2, player!.Name), ct);
            }
        }

        public sealed class NoSeedsForGuests(IHotel hotel) : IPurchasePolicy
        {
            public async ValueTask<PurchaseDecision> DecideAsync(PurchaseRequest request, PurchaseDecision proposed, CancellationToken ct) =>
                request.Offer.Id == 7 && await hotel.Players.ClubAsync(request.PlayerId, ct) is { Active: false }
                    ? PurchaseDecision.Refuse(error: CatalogPurchaseErrorType.RequiresHabboClub)
                    : proposed;
        }

        [Every("01:00:00")]
        public sealed class Harvest(IHotel hotel) : IScheduledTask
        {
            public async Task RunAsync(ScheduledTaskRun run, CancellationToken ct)
            {
                foreach (var player in hotel.Players.OnlineIds())
                    await hotel.Notify.NotificationAsync(player, "garden.harvest", new Dictionary<string, string>(), ct);
            }
        }

        public sealed class GardenRoute(IServiceScopeFactory scopes) : IPluginWebRoute
        {
            public string Method => "GET";

            public string Pattern => "plants";

            public async Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct)
            {
                await using var scope = scopes.CreateAsyncScope();

                return PluginWebResult.Ok(new { count = await scope.ServiceProvider.GetRequiredService<GardenDb>().Plants.CountAsync(ct) });
            }
        }

        [FurniLogic("garden_sprinkler")]
        public sealed class Sprinkler : FurniLogic
        {
            public override async Task OnWalkOnAsync(IFurniContext ctx, RoomUser user, CancellationToken ct)
            {
                await ctx.SetStateAsync(1, ct);
                await ctx.SayAsync(user, "Splash!", RoomSpeech.Talk, ct);
            }
        }
        """;

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-test-hotel-plugin-" + Guid.NewGuid().ToString("N")
    );

    private readonly TestHotel _hotel = TestHotel.Create();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        Write(SOURCE);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _hotel.DisposeAsync();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task A_plugin_that_does_not_compile_fails_its_load_with_the_servers_errors()
    {
        Write(SOURCE + "\npublic sealed class Broken { int x = \"no\"; }");

        var load = () => _hotel.LoadAsync(_folder, ct: Ct);

        (await load.Should().ThrowAsync<PluginLoadException>())
            .Which.Reasons.Should()
            .ContainSingle(x => x.Contains("CS0029"));
    }

    [Fact]
    public async Task A_plugin_doing_what_plugins_may_not_fails_its_load_as_the_policy_refuses_it()
    {
        Write(
            SOURCE
                + "\npublic static class Starts { public static void Go() => System.Diagnostics.Process.Start(\"calc\"); }"
        );

        var load = () => _hotel.LoadAsync(_folder, ct: Ct);

        (await load.Should().ThrowAsync<PluginLoadException>())
            .Which.Reasons.Should()
            .Contain(x => x.Contains("TPP005", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_command_runs_for_a_holder_of_its_node_saves_to_the_plugins_table_and_whispers_its_text()
    {
        await _hotel.LoadAsync(_folder, ct: Ct);
        _hotel.Settings.Set("Garden:Seeds", 5);

        var bob = _hotel.Player("bob").InRoom(5).WithPermissions("plugin.garden.plant");

        await bob.SaysAsync(":plant", Ct);
        await bob.SaysAsync(":plant", Ct);

        bob.Should().HaveHadCommand(CommandOutcome.Completed);
        bob.Should().HaveBeenWhispered("Planted 2 of 5.");
        var plants = await _hotel.Web.GetAsync("/plugins/garden/plants", bob, Ct);
        JsonSerializer.Serialize(plants.Value).Should().Be("""{"count":2}""");
    }

    [Fact]
    public async Task A_command_is_refused_to_a_player_without_its_node_with_the_hotels_reply()
    {
        await _hotel.LoadAsync(_folder, ct: Ct);

        var alice = _hotel.Player("alice").InRoom(5);

        await alice.SaysAsync(":plant", Ct);

        alice.Should().HaveHadCommand(CommandOutcome.Refused);
        alice.Whispers.Should().Equal(CommandReplyKeys.Defaults[CommandReplyKeys.NO_PERMISSION]);
    }

    [Fact]
    public async Task An_operator_command_runs_through_the_hotels_runner_waits_for_confirm_and_is_logged()
    {
        await _hotel.LoadAsync(_folder, ct: Ct);
        _hotel.Settings.Set("Turbo:Commands:ConfirmAtPlayers", 3);
        var bob = _hotel
            .Player("bob")
            .InRoom(5)
            .WithPermissions("plugin.garden.rain", PermissionNodes.Command.LOG);
        _hotel.Player("alice");
        _hotel.Player("carol");
        // With the room's owner, 4 players are online.

        await bob.SaysAsync(":rain", Ct);

        bob.Should().HaveHadCommand(CommandOutcome.AwaitingConfirmation);
        bob.Should()
            .HaveBeenWhispered(
                "That rains on 4 gardens. Type :confirm within 30 seconds to go ahead."
            );
        _hotel.Notify.Alerts.Should().BeEmpty();

        await bob.SaysAsync(":confirm", Ct);

        bob.Should().HaveBeenWhispered("It rained on 4 gardens.");
        _hotel
            .Notify.Alerts.Select(x => x.Text)
            .Should()
            .Equal(Enumerable.Repeat("Rain falls on your garden.", 4));
        _hotel
            .CommandLog.Entries.Select(x => (x.Command, x.Outcome))
            .Should()
            .Equal(("rain", "confirm"), ("rain", "completed"), ("confirm", "completed"));
    }

    [Fact]
    public async Task An_operator_command_finds_a_player_by_name_as_the_hotel_spells_it()
    {
        await _hotel.LoadAsync(_folder, ct: Ct);
        _hotel.Player("Carol");
        var bob = _hotel.Player("bob").InRoom(5).WithPermissions("plugin.garden.rain");

        await bob.SaysAsync(":drench CAROL", Ct);
        await bob.SaysAsync(":drench nobody", Ct);

        bob.Whispers.Should()
            .Equal(
                "Drenched the garden of Carol.",
                CommandReplyKeys
                    .Defaults[CommandReplyKeys.PLAYER_NOT_FOUND]
                    .Replace("%0%", "nobody")
            );
    }

    [Fact]
    public async Task A_message_from_the_client_reaches_the_plugins_handler_and_its_reply_reads_back_as_the_record()
    {
        var plugin = await _hotel.LoadAsync(_folder, ct: Ct);
        var bob = _hotel.Player("bob").InRoom(5);
        var water = (IPluginMessage)
            Activator.CreateInstance(plugin.Assembly.GetType("Garden.WaterMessage")!, 3)!;

        await bob.SendsAsync(water, Ct);

        bob.Received.Should()
            .ContainSingle()
            .Which.ToString()
            .Should()
            .Be("WateredMessage { Litres = 6, By = bob }");
    }

    [Fact]
    public async Task A_purchase_policy_refuses_with_the_hotels_error_and_lets_a_club_member_buy()
    {
        await _hotel.LoadAsync(_folder, ct: Ct);

        var guest = _hotel.Player("guest");
        var member = _hotel.Player("member");

        member.Fake.Club = new Turbo.Contracts.Hotel.HotelClub(Active: true, StreakDays: 1);

        await _hotel.Purchases.BuyAsync(guest, TestPurchases.Offer(7, credits: 5), ct: Ct);
        _hotel.Purchases.Should().HaveRefused(7, CatalogPurchaseErrorType.RequiresHabboClub);

        await _hotel.Purchases.BuyAsync(
            member,
            TestPurchases.Offer(7, credits: 5),
            quantity: 2,
            ct: Ct
        );
        _hotel.Purchases.Should().HaveAllowed(7).Decision.CostCredits.Should().Be(10);
    }

    [Fact]
    public async Task A_scheduled_task_runs_each_time_it_comes_due_as_the_clock_moves()
    {
        await _hotel.LoadAsync(_folder, ct: Ct);
        var bob = _hotel.Player("bob");

        await _hotel.Schedule.AdvanceAsync(TimeSpan.FromMinutes(59), Ct);
        bob.Notifications.Should().BeEmpty();

        await _hotel.Schedule.AdvanceAsync(TimeSpan.FromHours(2), Ct);
        _hotel
            .Schedule.Ran.Select(x =>
                x.At - new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
            )
            .Should()
            .Equal(TimeSpan.FromHours(1), TimeSpan.FromHours(2));
        bob.Should().HaveBeenNotified("garden.harvest");
    }

    [Fact]
    public async Task A_furni_runs_its_plugins_logic_when_walked_on()
    {
        await _hotel.LoadAsync(_folder, ct: Ct);
        var bob = _hotel.Player("bob").InRoom(5);
        var sprinkler = _hotel.Room(5).Furni("garden_sprinkler");

        await sprinkler.WalkOnAsync(bob, Ct);

        sprinkler.State.Should().Be(1);
        _hotel.Room(5).Should().HaveSaid("Splash!").Speaker.Should().Be("bob");
    }

    [Fact]
    public async Task A_second_hotel_takes_the_first_ones_compile_and_starts_from_nothing()
    {
        Write(SOURCE + "\npublic static class Visits { public static int Count; }");
        await using var first = TestHotel.Create();
        await using var second = TestHotel.Create();

        var before = await first.LoadAsync(_folder, ct: Ct);
        first.Settings.Set("Garden:Seeds", 5);
        await first
            .Player("bob")
            .InRoom(5)
            .WithPermissions("plugin.garden.plant")
            .SaysAsync(":plant", Ct);
        before.Assembly.GetType("Garden.Visits")!.GetField("Count")!.SetValue(null, 7);

        var after = await second.LoadAsync(_folder, ct: Ct);
        var bob = second.Player("bob").InRoom(5).WithPermissions("plugin.garden.plant");
        await bob.SaysAsync(":plant", Ct);

        second
            .Logs.Entries.Select(x => x.Message)
            .Should()
            .Contain(x => x.StartsWith("Plugin garden was compiled before in this process"))
            .And.NotContain(x => x.StartsWith("Compiled plugin garden"));
        after.Assembly.Should().NotBeSameAs(before.Assembly);
        after.Assembly.GetType("Garden.Visits")!.GetField("Count")!.GetValue(null).Should().Be(0);
        bob.Should().HaveBeenWhispered("Planted 1 of 3.");
    }

    private void Write(string source)
    {
        var server = Path.Combine(_folder, "server");

        if (Directory.Exists(server))
            Directory.Delete(server, recursive: true);

        Directory.CreateDirectory(server);
        File.WriteAllText(Path.Combine(_folder, "manifest.json"), MANIFEST);
        File.WriteAllText(Path.Combine(server, "Garden.cs"), source);
    }
}
