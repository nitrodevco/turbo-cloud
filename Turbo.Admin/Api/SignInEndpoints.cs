using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fido2NetLib;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Admin.Enums;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Signing in, from any browser: a passkey is the only way, and it needs no name typed, since the
/// passkey says whose it is. And making one from a setup link (<c>adminsetup</c>). Every endpoint
/// here is open, so all of them are rate limited per address.
/// </summary>
internal sealed class SignInEndpoints(
    IGrainFactory grainFactory,
    AdminPasskeys passkeys,
    ILogger<SignInEndpoints> logger
)
{
    private const string NO_ACCESS = "You do not have access to the admin panel.";
    private const string LINK_GONE =
        "That setup link has expired or was already used. Ask an admin for a new one.";
    private const string PASSKEY_FAILED = "That passkey did not sign you in. Try again.";

    /// <summary>Whom a sign-in prompt is for before a passkey answers it: nobody yet.</summary>
    private static readonly PlayerId NOBODY = new(0);

    public void Map(IEndpointRouteBuilder open)
    {
        var signIn = open.MapGroup("/api").RequireRateLimiting(AdminResults.SIGN_IN_POLICY);

        signIn.MapPost("/auth/options", SignInOptionsAsync);
        signIn.MapPost("/auth/passkey", PasskeyAsync);
        signIn.MapPost("/setup/info", SetupInfoAsync);
        signIn.MapPost("/setup/passkey", SetupPasskeyAsync);
        signIn.MapPost("/setup/complete", SetupCompleteAsync);
    }

    /// <summary>A passkey prompt for whichever of the device's panel passkeys the person picks.</summary>
    private async Task<IResult> SignInOptionsAsync(CancellationToken ct)
    {
        var options = passkeys.BeginSignIn([]);
        var ceremonyId = await grainFactory
            .GetAdminAuthGrain()
            .BeginCeremonyAsync(NOBODY, AdminCeremonyKind.SignIn, options.ToJson(), ct)
            .ConfigureAwait(false);

        return AdminResults.Ceremony(ceremonyId, options.ToJson());
    }

    /// <summary>The passkey's answer: it names its player, whose stored key must check out.</summary>
    private async Task<IResult> PasskeyAsync(PasskeyAnswerRequest request, CancellationToken ct)
    {
        var ceremony = await grainFactory
            .GetAdminAuthGrain()
            .TakeCeremonyAsync(request.CeremonyId ?? string.Empty, AdminCeremonyKind.SignIn, ct)
            .ConfigureAwait(false);

        if (ceremony is null || ceremony.PlayerId != NOBODY)
            return AdminResults.Error(StatusCodes.Status401Unauthorized, PASSKEY_FAILED);

        PlayerId playerId;

        try
        {
            var answer = request.Credential.Deserialize<AuthenticatorAssertionRawResponse>()!;

            if (AdminPasskeys.PlayerOf(answer.Response?.UserHandle) is not { } owner)
                return AdminResults.Error(StatusCodes.Status401Unauthorized, PASSKEY_FAILED);

            playerId = owner;

            if (!await CheckAsync(playerId, ceremony.OptionsJson, answer, ct).ConfigureAwait(false))
                return AdminResults.Error(StatusCodes.Status401Unauthorized, PASSKEY_FAILED);
        }
        catch (Exception ex)
            when (ex is Fido2VerificationException or JsonException or ArgumentException)
        {
            logger.LogInformation(ex, "A passkey sign-in failed");

            return AdminResults.Error(StatusCodes.Status401Unauthorized, PASSKEY_FAILED);
        }

        return await OpenSessionAsync(playerId, ct).ConfigureAwait(false);
    }

    private async Task<IResult> SetupInfoAsync(SetupTokenRequest request, CancellationToken ct)
    {
        if (await SetupPlayerAsync(request.Token, ct).ConfigureAwait(false) is not { } playerId)
            return AdminResults.Error(StatusCodes.Status401Unauthorized, LINK_GONE);

        var name = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNameAsync(playerId, ct)
            .ConfigureAwait(false);
        var account = await grainFactory
            .GetAdminAccountGrain(playerId)
            .GetAccountAsync(ct)
            .ConfigureAwait(false);

        return Results.Ok(new SetupInfoResponse(name, account.Passkeys.Length > 0));
    }

    private async Task<IResult> SetupPasskeyAsync(SetupTokenRequest request, CancellationToken ct)
    {
        if (await SetupPlayerAsync(request.Token, ct).ConfigureAwait(false) is not { } playerId)
            return AdminResults.Error(StatusCodes.Status401Unauthorized, LINK_GONE);

        var name = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNameAsync(playerId, ct)
            .ConfigureAwait(false);
        // The new passkey replaces all of them, so none is excluded.
        var options = passkeys.BeginRegistration(playerId, name, []);
        var ceremonyId = await grainFactory
            .GetAdminAuthGrain()
            .BeginCeremonyAsync(playerId, AdminCeremonyKind.Register, options.ToJson(), ct)
            .ConfigureAwait(false);

        return AdminResults.Ceremony(ceremonyId, options.ToJson());
    }

    /// <summary>Finishes a setup link: checks the new passkey, spends the link, and signs in.</summary>
    private async Task<IResult> SetupCompleteAsync(
        SetupCompleteRequest request,
        CancellationToken ct
    )
    {
        var auth = grainFactory.GetAdminAuthGrain();

        if (await SetupPlayerAsync(request.Token, ct).ConfigureAwait(false) is not { } playerId)
            return AdminResults.Error(StatusCodes.Status401Unauthorized, LINK_GONE);

        var ceremony = await auth.TakeCeremonyAsync(
                request.CeremonyId ?? string.Empty,
                AdminCeremonyKind.Register,
                ct
            )
            .ConfigureAwait(false);

        if (ceremony is null || ceremony.PlayerId != playerId)
            return AdminResults.Error(StatusCodes.Status400BadRequest, PASSKEY_FAILED);

        AdminPasskeyCredential passkey;

        try
        {
            passkey = await passkeys
                .CompleteRegistrationAsync(
                    ceremony.OptionsJson,
                    request.Credential.Deserialize<AuthenticatorAttestationRawResponse>()!,
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is Fido2VerificationException or JsonException or ArgumentException)
        {
            logger.LogInformation(ex, "A passkey setup for player {PlayerId} failed", playerId);

            return AdminResults.Error(StatusCodes.Status400BadRequest, PASSKEY_FAILED);
        }

        // Spent only now, so a passkey prompt cancelled halfway leaves the link to try again.
        if (await auth.RedeemSetupTokenAsync(request.Token, ct).ConfigureAwait(false) != playerId)
            return AdminResults.Error(StatusCodes.Status401Unauthorized, LINK_GONE);

        await grainFactory
            .GetAdminAccountGrain(playerId)
            .ReplacePasskeysAsync(passkey, request.PasskeyName ?? string.Empty, ct)
            .ConfigureAwait(false);

        logger.LogInformation("Player {PlayerId} set up an admin panel passkey", playerId);

        return await OpenSessionAsync(playerId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks a passkey answer against the player's stored passkey of that id, and records its use.
    /// False when the player has no such passkey; throws when the answer does not check out.
    /// </summary>
    private async Task<bool> CheckAsync(
        PlayerId playerId,
        string optionsJson,
        AuthenticatorAssertionRawResponse answer,
        CancellationToken ct
    )
    {
        var account = grainFactory.GetAdminAccountGrain(playerId);
        var stored = (
            await account.GetPasskeyCredentialsAsync(ct).ConfigureAwait(false)
        ).FirstOrDefault(x => x.CredentialId.AsSpan().SequenceEqual(answer.RawId));

        if (stored is null)
            return false;

        var counter = await passkeys
            .CompleteSignInAsync(optionsJson, answer, stored, playerId, ct)
            .ConfigureAwait(false);

        await account.RecordPasskeyUseAsync(stored.CredentialId, counter, ct).ConfigureAwait(false);

        return true;
    }

    private Task<PlayerId?> SetupPlayerAsync(string? token, CancellationToken ct) =>
        grainFactory.GetAdminAuthGrain().GetSetupPlayerAsync(token ?? string.Empty, ct);

    /// <summary>Opens a session for a player who has proved who they are, if they may sign in.</summary>
    private async Task<IResult> OpenSessionAsync(PlayerId playerId, CancellationToken ct)
    {
        if (
            !await grainFactory
                .HasPermissionAsync(playerId, PermissionNodes.Admin.PANEL, ct)
                .ConfigureAwait(false)
        )
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);

        var grant = await grainFactory
            .GetAdminAuthGrain()
            .OpenSessionAsync(playerId, ct)
            .ConfigureAwait(false);
        var name = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNameAsync(playerId, ct)
            .ConfigureAwait(false);

        return Results.Ok(
            new SessionResponse(
                grant.SessionToken,
                grant.Session.ExpiresAtUtc,
                new AdminPlayer(playerId.Value, name)
            )
        );
    }
}
