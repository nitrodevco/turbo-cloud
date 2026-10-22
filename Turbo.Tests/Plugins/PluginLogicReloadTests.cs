using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// A plugin's furni logic in a room that stays loaded while the plugin reloads: the room's next
/// tick gives each of its furni the new load's logic, not the old one whose services the reload
/// disposed; once the plugin is gone they are plain furni, and they take its logic again when it
/// comes back.
/// </summary>
public sealed class PluginLogicReloadTests
{
    private const string LOGIC = "test_plugin_box";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_loaded_room_rebinds_a_plugins_furni_when_the_plugin_reloads_unloads_and_loads()
    {
        var room = new WiredRoom();
        var provider = room.Harness.LogicProvider;
        var first = provider.RegisterLogic(LOGIC, room.Harness.Services, Make<OldLoadLogic>);
        var item = room.AddFloorItem(
            1,
            2,
            2,
            LOGIC,
            createLogic: (_, ctx) => provider.CreateLogicInstance(LOGIC, ctx)
        );
        await TickAsync(room);

        var old = item.Logic.Should().BeOfType<OldLoadLogic>().Subject;

        await TickAsync(room);
        item.Logic.Should().BeSameAs(old, "nothing was registered since");

        // The plugin reloads: its old load's registration goes, the new load's comes.
        first.Dispose();
        using (provider.RegisterLogic(LOGIC, room.Harness.Services, Make<NewLoadLogic>))
        {
            await TickAsync(room);

            item.Logic.Should().BeOfType<NewLoadLogic>();
            old.Detached.Should()
                .BeTrue("the old logic is told it is detached, and its failure kept from the room");
            room.Harness.State.ItemsById.Should().ContainKey(item.ObjectId);
        }

        // Unloaded: a plain furni, not a logic whose plugin is gone.
        await TickAsync(room);
        item.Logic.GetType().Should().Be<FurnitureFloorLogic>();

        // Loaded again: its logic comes back.
        using (provider.RegisterLogic(LOGIC, room.Harness.Services, Make<NewLoadLogic>))
        {
            await TickAsync(room);

            item.Logic.Should().BeOfType<NewLoadLogic>();
        }
    }

    /// <summary>What the room's tick starts with.</summary>
    private static Task TickAsync(WiredRoom room) =>
        room.Harness.Room.ObjectModule.RebindStaleLogicsAsync(Ct);

    private static IRoomObjectLogic Make<T>(IServiceProvider sp, IRoomObjectContext ctx)
        where T : IRoomObjectLogic =>
        (T)
            Activator.CreateInstance(
                typeof(T),
                sp.GetRequiredService<IStuffDataFactory>(),
                (IRoomFloorItemContext)ctx
            )!;

    /// <summary>A logic whose plugin's services were disposed by the reload.</summary>
    private sealed class OldLoadLogic(IStuffDataFactory stuffData, IRoomFloorItemContext ctx)
        : FurnitureFloorLogic(stuffData, ctx)
    {
        public bool Detached { get; private set; }

        public override Task OnDetachAsync(CancellationToken ct)
        {
            Detached = true;

            throw new ObjectDisposedException("the old load's services");
        }
    }

    private sealed class NewLoadLogic(IStuffDataFactory stuffData, IRoomFloorItemContext ctx)
        : FurnitureFloorLogic(stuffData, ctx);
}
