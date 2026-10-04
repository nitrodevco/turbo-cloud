using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Admin.Snapshots;

namespace Turbo.Primitives.Admin.Grains;

/// <summary>
/// One player's admin panel passkeys, keyed by player id: the panel's only way to sign in. There
/// is no password and nothing to recover one with; a lost passkey is replaced through a link an
/// admin issues. The grain owns the <c>admin_passkeys</c> rows; nothing else writes them.
/// </summary>
public interface IAdminAccountGrain : IGrainWithIntegerKey
{
    Task<AdminAccountSnapshot> GetAccountAsync(CancellationToken ct);

    Task<ImmutableArray<AdminPasskeyCredential>> GetPasskeyCredentialsAsync(CancellationToken ct);

    /// <summary>Notes a passkey was just used, with its authenticator's new signature counter.</summary>
    Task RecordPasskeyUseAsync(byte[] credentialId, uint signCount, CancellationToken ct);

    /// <summary>
    /// Makes this passkey the player's only one, from a setup link: a first passkey, or one that
    /// replaces a lost device, whose passkeys go with it.
    /// </summary>
    Task ReplacePasskeysAsync(AdminPasskeyCredential passkey, string name, CancellationToken ct);

    Task AddPasskeyAsync(AdminPasskeyCredential passkey, string name, CancellationToken ct);

    /// <summary>False when it is the player's last passkey, which would lock them out.</summary>
    Task<bool> RemovePasskeyAsync(int passkeyId, CancellationToken ct);
}
