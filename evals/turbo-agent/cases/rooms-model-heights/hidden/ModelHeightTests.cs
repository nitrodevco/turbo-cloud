using EvalHarness;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Providers;
using Xunit;

namespace EvalHidden;

/// <summary>
/// Hidden regression tests for room model heights, driven through the real RoomModelProvider
/// (model rows loaded from a database, compiled, looked up) and the effective RoomConfig.
/// </summary>
public class ModelHeightTests
{
    private static async Task<Turbo.Primitives.Rooms.Snapshots.Mapping.RoomModelSnapshot> Load(string model)
    {
        var db = new InMemoryDb();
        await using (var ctx = db.CreateDbContext())
        {
            ctx.RoomModels.Add(
                new RoomModelEntity
                {
                    Id = 7,
                    Name = "eval_model",
                    Model = model,
                    DoorX = 0,
                    DoorY = 1,
                    DoorRotation = Rotation.South,
                    Enabled = true,
                    Custom = false,
                }
            );
            await ctx.SaveChangesAsync();
        }
        var provider = new RoomModelProvider(db, NullLogger<IRoomModelProvider>.Instance);
        await provider.ReloadAsync(CancellationToken.None);
        return provider.GetModelById(7);
    }

    [Fact]
    public async Task DigitTiles_AreWholeTileHeights()
    {
        var m = await Load("0123\r\n4567\r\n89xx");
        var expected = new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        for (var i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], m.BaseHeights[i].Value, 3);
    }

    [Fact]
    public async Task LetterTiles_ContinueAfterNine()
    {
        var m = await Load("abcz\r\n0000");
        Assert.Equal(10.0, m.BaseHeights[0].Value, 3);
        Assert.Equal(11.0, m.BaseHeights[1].Value, 3);
        Assert.Equal(12.0, m.BaseHeights[2].Value, 3);
        Assert.Equal(35.0, m.BaseHeights[3].Value, 3);
    }

    [Fact]
    public async Task VoidTiles_StayUnusable()
    {
        var m = await Load("x1\r\n1x");
        Assert.True(m.BaseFlags[0].HasFlag(RoomTileFlags.Disabled));
        Assert.True(m.BaseFlags[3].HasFlag(RoomTileFlags.Disabled));
        Assert.Equal(1.0, m.BaseHeights[1].Value, 3);
        Assert.False(m.BaseFlags[1].HasFlag(RoomTileFlags.Disabled));
    }

    [Fact]
    public void RoomWithoutExplicitWallHeight_GetsAutomaticWalls()
    {
        // The hotel default is whatever a running server would bind: the shipped appsettings
        // section over the option's own default.
        var config = new RoomConfig();
        var ws = Environment.GetEnvironmentVariable("EVAL_WS");
        var json = ws is null ? null : Path.Combine(ws, "appsettings.json");
        if (json is not null && File.Exists(json))
        {
            var root = new ConfigurationBuilder().AddJsonFile(json, optional: true).Build();
            root.GetSection(RoomConfig.SECTION_NAME).Bind(config);
        }
        Assert.Equal(-1, config.DefaultWallHeight);
    }
}
