using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Admin.Enums;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Admin.Grains;

/// <summary>
/// Who is signed in to the admin panel, and the short-lived steps on the way there:
/// <list type="bullet">
/// <item>setup links (<c>adminsetup</c>): a single-use token that lets a player make the passkey
/// they sign in with, a first one or one that replaces those they lost;</item>
/// <item>passkey ceremonies: the options a browser was given, held until it answers.</item>
/// </list>
/// Sessions are kept in the database so a restart signs nobody out; links and ceremonies are not,
/// so a restart voids every open one. Only hashes are kept, in memory and in the table, so a heap
/// dump or a copy of the database holds no usable token. The passkeys themselves are each
/// player's <see cref="IAdminAccountGrain"/>.
/// <para>
/// A session says who someone is, never what they may do: the API checks the permission nodes
/// on every request, so a revoked node takes effect at once.
/// </para>
/// </summary>
public interface IAdminAuthGrain : IGrainWithStringKey
{
    /// <summary>A new single-use setup token for the player, valid for some hours.</summary>
    Task<AdminSetupLinkSnapshot> CreateSetupTokenAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>Whose setup token this is, without spending it; null when unknown or expired.</summary>
    Task<PlayerId?> GetSetupPlayerAsync(string setupToken, CancellationToken ct);

    /// <summary>Spends a setup token; null when it is unknown, used or expired.</summary>
    Task<PlayerId?> RedeemSetupTokenAsync(string setupToken, CancellationToken ct);

    /// <summary>
    /// Holds a passkey ceremony's options for a few minutes and returns its id. A sign-in from
    /// the sign-in page is for nobody yet (<see cref="PlayerId"/> 0): the passkey says who.
    /// </summary>
    Task<string> BeginCeremonyAsync(
        PlayerId playerId,
        AdminCeremonyKind kind,
        string optionsJson,
        CancellationToken ct
    );

    /// <summary>
    /// Takes a ceremony back to check the browser's answer. A ceremony is taken once, whatever
    /// the answer turns out to be; null when it is unknown, expired or of another kind.
    /// </summary>
    Task<AdminCeremonySnapshot?> TakeCeremonyAsync(
        string ceremonyId,
        AdminCeremonyKind kind,
        CancellationToken ct
    );

    /// <summary>Opens a session for a player who has just proved who they are.</summary>
    Task<AdminSessionGrant> OpenSessionAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>The session a token belongs to; null when it is unknown or expired.</summary>
    Task<AdminSessionSnapshot?> GetSessionAsync(string sessionToken, CancellationToken ct);

    /// <summary>The player's live sessions, newest first.</summary>
    Task<ImmutableArray<AdminSessionSnapshot>> GetPlayerSessionsAsync(
        PlayerId playerId,
        CancellationToken ct
    );

    Task EndSessionAsync(string sessionToken, CancellationToken ct);

    /// <summary>
    /// Ends one of the player's sessions by its <see cref="AdminSessionSnapshot.Id"/>, or all of
    /// them when <paramref name="sessionId"/> is null; returns how many ended. Another player's
    /// session is never ended by its id here.
    /// </summary>
    Task<int> EndPlayerSessionsAsync(PlayerId playerId, int? sessionId, CancellationToken ct);
}
