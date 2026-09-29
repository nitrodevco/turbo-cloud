using EvalHarness;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Xunit;

namespace EvalHidden;

/// <summary>
/// Hidden regression tests: a batch name lookup leaves out players that do not exist, however
/// many ids it is asked for, and still answers every player that does.
/// </summary>
public class NameLookupTests
{
    private static async Task<IPlayerDirectoryGrain> Directory()
    {
        var db = new InMemoryDb();
        await using (var ctx = db.CreateDbContext())
        {
            foreach (var (id, name) in new[] { (1, "alice"), (2, "bob") })
                ctx.Players.Add(new PlayerEntity
                {
                    Id = id, Name = name, Figure = "hd-180-1",
                    Gender = default, PlayerStatus = default, PlayerPerks = default,
                });
            await ctx.SaveChangesAsync();
        }
        return (IPlayerDirectoryGrain)GrainHarness.Create(
            typeof(Turbo.Players.PlayerModule).Assembly,
            "Turbo.Players.Grains.PlayerDirectoryGrain",
            new Fakes(),
            db
        );
    }

    [Fact]
    public async Task SingleUnknownId_IsLeftOut()
    {
        var dir = await Directory();
        var names = await dir.GetPlayerNamesAsync([(PlayerId)999], CancellationToken.None);
        Assert.Empty(names);
    }

    [Fact]
    public async Task SingleUnknownId_SameAnswerAsInABatch()
    {
        var dir = await Directory();
        var one = await dir.GetPlayerNamesAsync([(PlayerId)999], CancellationToken.None);
        var many = await dir.GetPlayerNamesAsync([(PlayerId)999, (PlayerId)998], CancellationToken.None);
        Assert.Equal(many.ContainsKey(999), one.ContainsKey(999));
    }

    [Fact]
    public async Task SingleKnownId_IsAnswered()
    {
        var dir = await Directory();
        var names = await dir.GetPlayerNamesAsync([(PlayerId)1], CancellationToken.None);
        Assert.Equal("alice", Assert.Single(names).Value);
    }

    [Fact]
    public async Task MixedBatch_AnswersOnlyKnownPlayers_Deduplicated()
    {
        var dir = await Directory();
        var names = await dir.GetPlayerNamesAsync(
            [(PlayerId)1, (PlayerId)999, (PlayerId)2, (PlayerId)1], CancellationToken.None);
        Assert.Equal(2, names.Count);
        Assert.Equal("alice", names[1]);
        Assert.Equal("bob", names[2]);
    }
}
