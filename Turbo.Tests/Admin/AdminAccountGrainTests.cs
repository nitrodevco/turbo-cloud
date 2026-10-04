using System.Reflection;
using FluentAssertions;
using Orleans;
using Orleans.Runtime;
using Turbo.Admin.Configuration;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Admin.Grains;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// A staff member's admin panel passkeys, the panel's only way in: a setup link makes one and
/// replaces the rest, more can be added, and the last one can never be removed.
/// </summary>
public sealed class AdminAccountGrainTests : IDisposable
{
    private const int PLAYER = 7;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _time = new(
        new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero)
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminAccountGrainTests() =>
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "staff",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task APlayerStartsWithNoPasskeys()
    {
        (await Grain().GetAccountAsync(Ct)).Passkeys.Should().BeEmpty();
    }

    [Fact]
    public async Task ASetupLinkMakesThePlayersPasskey()
    {
        var grain = Grain();

        await grain.ReplacePasskeysAsync(Passkey(1), "Laptop", Ct);

        (await grain.GetAccountAsync(Ct))
            .Passkeys.Should()
            .ContainSingle()
            .Which.Name.Should()
            .Be("Laptop");
        (await grain.GetPasskeyCredentialsAsync(Ct))
            .Single()
            .CredentialId.Should()
            .Equal(Passkey(1).CredentialId);
    }

    [Fact]
    public async Task AReplacingLinkRemovesEveryOldPasskey()
    {
        var grain = Grain();
        await grain.ReplacePasskeysAsync(Passkey(1), "Lost phone", Ct);
        await grain.AddPasskeyAsync(Passkey(2), "Stolen laptop", Ct);

        await grain.ReplacePasskeysAsync(Passkey(3), "New phone", Ct);

        (await grain.GetPasskeyCredentialsAsync(Ct))
            .Should()
            .ContainSingle()
            .Which.CredentialId.Should()
            .Equal(Passkey(3).CredentialId);
    }

    [Fact]
    public async Task MorePasskeysCanBeAdded()
    {
        var grain = Grain();
        await grain.ReplacePasskeysAsync(Passkey(1), "Laptop", Ct);

        await grain.AddPasskeyAsync(Passkey(2), "Phone", Ct);

        (await grain.GetAccountAsync(Ct))
            .Passkeys.Select(x => x.Name)
            .Should()
            .Equal("Laptop", "Phone");
    }

    [Fact]
    public async Task TheLastPasskeyCannotBeRemoved()
    {
        var grain = Grain();
        await grain.ReplacePasskeysAsync(Passkey(1), "Laptop", Ct);
        await grain.AddPasskeyAsync(Passkey(2), "Phone", Ct);
        var passkeys = (await grain.GetAccountAsync(Ct)).Passkeys;

        (await grain.RemovePasskeyAsync(passkeys[0].Id, Ct)).Should().BeTrue();
        (await grain.RemovePasskeyAsync(passkeys[1].Id, Ct)).Should().BeFalse();
        (await grain.GetAccountAsync(Ct)).Passkeys.Should().ContainSingle();
    }

    [Fact]
    public async Task UsingAPasskeyRecordsItsCounterAndWhen()
    {
        var grain = Grain();
        await grain.ReplacePasskeysAsync(Passkey(1), "Laptop", Ct);

        await grain.RecordPasskeyUseAsync(Passkey(1).CredentialId, 42, Ct);

        (await grain.GetPasskeyCredentialsAsync(Ct)).Single().SignCount.Should().Be(42u);
        (await grain.GetAccountAsync(Ct))
            .Passkeys.Single()
            .LastUsedAtUtc.Should()
            .Be(_time.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task AnUnnamedPasskeyGetsAName()
    {
        var grain = Grain();

        await grain.ReplacePasskeysAsync(Passkey(1), "   ", Ct);

        (await grain.GetAccountAsync(Ct)).Passkeys.Single().Name.Should().Be("Passkey");
    }

    private static AdminPasskeyCredential Passkey(byte seed) =>
        new()
        {
            CredentialId = [seed, seed, seed, seed, 1, 2, 3, 4],
            PublicKey = [seed, 9, 9, 9],
            SignCount = 0,
        };

    private IAdminAccountGrain Grain()
    {
        var type = typeof(AdminConfig).Assembly.GetType("Turbo.Admin.Grains.AdminAccountGrain")!;
        var grain = (Grain)
            Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                [_db, _time],
                culture: null
            )!;

        // The grain reads its player from its key, which comes from its context.
        _fakes.Handlers["get_GrainId"] = _ =>
            GrainId.Create(
                GrainType.Create("adminaccount"),
                GrainIdKeyExtensions.CreateIntegerKey(PLAYER)
            );
        typeof(Grain)
            .GetProperty(
                "GrainContext",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )!
            .GetSetMethod(true)!
            .Invoke(grain, [_fakes.Create<IGrainContext>("adminaccount")]);

        return (IAdminAccountGrain)grain;
    }
}
