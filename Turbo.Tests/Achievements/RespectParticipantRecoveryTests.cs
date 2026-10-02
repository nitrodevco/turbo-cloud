using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class RespectParticipantRecoveryTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RespectParticipantRecoveryTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "respect-recovery",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
                RespectsLeft = 1,
                PetRespectsLeft = 1,
                RespectResetDate = DateTime.UtcNow.Date,
                RespectPoints = 4,
            }
        );
    }

    [Fact]
    public async Task ReactivationReplaysBothQuotaKindsAndRecipientWithoutRepeatedMutations()
    {
        var first = NewGrain();
        Assert.True(await first.SpendRespectOperationAsync("human", Ct));
        Assert.True(await first.SpendPetRespectOperationAsync("pet", Ct));
        Assert.Equal(5, await first.ReceiveRespectOperationAsync("receive", Ct));
        var recovered = NewGrain();
        Assert.True(await recovered.SpendRespectOperationAsync("human", Ct));
        Assert.True(await recovered.SpendPetRespectOperationAsync("pet", Ct));
        Assert.Equal(5, await recovered.ReceiveRespectOperationAsync("receive", Ct));
        await using var db = await _db.CreateDbContextAsync(Ct);
        var player = await db.Players.SingleAsync(Ct);
        Assert.Equal(0, player.RespectsLeft);
        Assert.Equal(0, player.PetRespectsLeft);
        Assert.Equal(5, player.RespectPoints);
        Assert.Equal(3, await db.HumanRespectParticipantReceipts.CountAsync(Ct));
    }

    [Fact]
    public async Task DailyResetDoesNotTurnARejectedOperationIntoANewSpend()
    {
        var grain = NewGrain();
        Assert.True(await grain.SpendRespectOperationAsync("last-human", Ct));
        Assert.False(await grain.SpendRespectOperationAsync("rejected-human", Ct));
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            (await db.Players.SingleAsync(Ct)).RespectResetDate = DateTime.UtcNow.Date.AddDays(-1);
            await db.SaveChangesAsync(Ct);
        }
        var recovered = NewGrain();
        Assert.False(await recovered.SpendRespectOperationAsync("rejected-human", Ct));
        Assert.True(await recovered.SpendRespectOperationAsync("new-day-human", Ct));
        await using var after = await _db.CreateDbContextAsync(Ct);
        var player = await after.Players.SingleAsync(Ct);
        Assert.Equal(2, player.RespectsLeft);
        Assert.Equal(3, player.PetRespectsLeft);
    }

    private IPlayerGrain NewGrain() =>
        (IPlayerGrain)
            GrainHarness.Create(
                typeof(PlayerModule).Assembly,
                "Turbo.Players.Grains.PlayerGrain",
                new Fakes(),
                _db,
                1
            );

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
