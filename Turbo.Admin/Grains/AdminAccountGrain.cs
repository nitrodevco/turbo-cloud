using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Admin;
using Turbo.Primitives.Admin.Grains;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Grains;

/// <summary>
/// One player's admin panel passkeys (see <see cref="IAdminAccountGrain"/>). Every call reads the
/// rows afresh: sign-ins are rare, and the grain's turn-by-turn execution keeps a replace and a
/// remove from crossing.
/// </summary>
internal sealed class AdminAccountGrain(
    IDbContextFactory<TurboDbContext> database,
    TimeProvider timeProvider
) : Grain, IAdminAccountGrain
{
    private const int MAX_NAME_LENGTH = 64;
    private const string DEFAULT_NAME = "Passkey";

    private PlayerId PlayerId => this.GetPlayerId();

    public async Task<AdminAccountSnapshot> GetAccountAsync(CancellationToken ct)
    {
        await using var db = await database.CreateDbContextAsync(ct);

        var passkeys = await db
            .AdminPasskeys.Where(x => x.PlayerEntityId == PlayerId.Value)
            .OrderBy(x => x.Id)
            .Select(x => new AdminPasskeySnapshot
            {
                Id = x.Id,
                Name = x.Name,
                CreatedAtUtc = x.CreatedAt,
                LastUsedAtUtc = x.LastUsedAt,
            })
            .ToListAsync(ct);

        return new AdminAccountSnapshot { Passkeys = [.. passkeys] };
    }

    public async Task<ImmutableArray<AdminPasskeyCredential>> GetPasskeyCredentialsAsync(
        CancellationToken ct
    )
    {
        await using var db = await database.CreateDbContextAsync(ct);

        var passkeys = await db
            .AdminPasskeys.Where(x => x.PlayerEntityId == PlayerId.Value)
            .Select(x => new AdminPasskeyCredential
            {
                CredentialId = x.CredentialId,
                PublicKey = x.PublicKey,
                SignCount = (uint)x.SignCount,
                AaGuid = x.AaGuid,
            })
            .ToListAsync(ct);

        return [.. passkeys];
    }

    public async Task RecordPasskeyUseAsync(
        byte[] credentialId,
        uint signCount,
        CancellationToken ct
    )
    {
        await using var db = await database.CreateDbContextAsync(ct);

        var passkey = await db.AdminPasskeys.FirstOrDefaultAsync(
            x => x.PlayerEntityId == PlayerId.Value && x.CredentialId == credentialId,
            ct
        );

        if (passkey is null)
            return;

        passkey.SignCount = signCount;
        passkey.LastUsedAt = timeProvider.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);
    }

    public async Task ReplacePasskeysAsync(
        AdminPasskeyCredential passkey,
        string name,
        CancellationToken ct
    )
    {
        await using var db = await database.CreateDbContextAsync(ct);

        // A replacing link is for a lost device, which may be in somebody else's hands, so every
        // passkey goes and only the new one signs in.
        db.AdminPasskeys.RemoveRange(
            await db.AdminPasskeys.Where(x => x.PlayerEntityId == PlayerId.Value).ToListAsync(ct)
        );
        db.AdminPasskeys.Add(ToEntity(passkey, name));

        await db.SaveChangesAsync(ct);
    }

    public async Task AddPasskeyAsync(
        AdminPasskeyCredential passkey,
        string name,
        CancellationToken ct
    )
    {
        await using var db = await database.CreateDbContextAsync(ct);

        db.AdminPasskeys.Add(ToEntity(passkey, name));

        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> RemovePasskeyAsync(int passkeyId, CancellationToken ct)
    {
        await using var db = await database.CreateDbContextAsync(ct);

        var passkeys = await db
            .AdminPasskeys.Where(x => x.PlayerEntityId == PlayerId.Value)
            .ToListAsync(ct);

        if (
            passkeys.Count <= 1
            || passkeys.FirstOrDefault(x => x.Id == passkeyId) is not { } passkey
        )
            return false;

        db.AdminPasskeys.Remove(passkey);

        await db.SaveChangesAsync(ct);

        return true;
    }

    private AdminPasskeyEntity ToEntity(AdminPasskeyCredential passkey, string name)
    {
        var trimmed = name.Trim();

        return new AdminPasskeyEntity
        {
            PlayerEntityId = PlayerId.Value,
            CredentialId = passkey.CredentialId,
            PublicKey = passkey.PublicKey,
            SignCount = passkey.SignCount,
            AaGuid = passkey.AaGuid,
            Name =
                trimmed.Length == 0 ? DEFAULT_NAME
                : trimmed.Length > MAX_NAME_LENGTH ? trimmed[..MAX_NAME_LENGTH]
                : trimmed,
        };
    }
}
