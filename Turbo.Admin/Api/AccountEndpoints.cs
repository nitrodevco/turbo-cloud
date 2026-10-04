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
using Turbo.Primitives.Orleans;

namespace Turbo.Admin.Api;

/// <summary>
/// The signed-in player's passkeys. Adding one takes a fresh answer from a passkey they already
/// have, so a session left open on a shared computer cannot be turned into a way back in.
/// </summary>
internal sealed class AccountEndpoints(
    IGrainFactory grainFactory,
    AdminPasskeys passkeys,
    ILogger<AccountEndpoints> logger
)
{
    private const string PASSKEY_FAILED = "The passkey could not be checked. Try again.";
    private const string LAST_PASSKEY = "Keep at least one passkey, or you could not sign in.";

    public void Map(RouteGroupBuilder secured)
    {
        secured.MapGet("/account", GetAsync);
        secured.MapPost("/account/confirm", ConfirmOptionsAsync);
        secured.MapPost("/account/passkeys/options", PasskeyOptionsAsync);
        secured.MapPost("/account/passkeys", AddPasskeyAsync);
        secured.MapDelete("/account/passkeys/{id:int}", RemovePasskeyAsync);
    }

    private async Task<IResult> GetAsync(HttpContext http, CancellationToken ct)
    {
        var account = await grainFactory
            .GetAdminAccountGrain(AdminIdentity.Of(http).PlayerId)
            .GetAccountAsync(ct)
            .ConfigureAwait(false);

        return Results.Ok(
            new AccountResponse([
                .. account.Passkeys.Select(x => new AccountPasskey(
                    x.Id,
                    x.Name,
                    x.CreatedAtUtc,
                    x.LastUsedAtUtc
                )),
            ])
        );
    }

    /// <summary>A passkey prompt, for one of the player's own passkeys, to confirm it is them.</summary>
    private async Task<IResult> ConfirmOptionsAsync(HttpContext http, CancellationToken ct)
    {
        var identity = AdminIdentity.Of(http);
        var credentials = await grainFactory
            .GetAdminAccountGrain(identity.PlayerId)
            .GetPasskeyCredentialsAsync(ct)
            .ConfigureAwait(false);
        var options = passkeys.BeginSignIn(credentials.Select(x => x.CredentialId));
        var ceremonyId = await grainFactory
            .GetAdminAuthGrain()
            .BeginCeremonyAsync(identity.PlayerId, AdminCeremonyKind.SignIn, options.ToJson(), ct)
            .ConfigureAwait(false);

        return AdminResults.Ceremony(ceremonyId, options.ToJson());
    }

    /// <summary>A prompt to make another passkey, once an existing one has confirmed it is them.</summary>
    private async Task<IResult> PasskeyOptionsAsync(
        HttpContext http,
        AddPasskeyOptionsRequest request,
        CancellationToken ct
    )
    {
        var identity = AdminIdentity.Of(http);
        var account = grainFactory.GetAdminAccountGrain(identity.PlayerId);
        var confirm = await grainFactory
            .GetAdminAuthGrain()
            .TakeCeremonyAsync(request.CeremonyId ?? string.Empty, AdminCeremonyKind.SignIn, ct)
            .ConfigureAwait(false);

        if (confirm is null || confirm.PlayerId != identity.PlayerId)
            return AdminResults.Error(StatusCodes.Status400BadRequest, PASSKEY_FAILED);

        var credentials = await account.GetPasskeyCredentialsAsync(ct).ConfigureAwait(false);

        try
        {
            var answer = request.Credential.Deserialize<AuthenticatorAssertionRawResponse>()!;
            var stored = credentials.FirstOrDefault(x =>
                x.CredentialId.AsSpan().SequenceEqual(answer.RawId)
            );

            if (stored is null)
                return AdminResults.Error(StatusCodes.Status400BadRequest, PASSKEY_FAILED);

            var counter = await passkeys
                .CompleteSignInAsync(confirm.OptionsJson, answer, stored, identity.PlayerId, ct)
                .ConfigureAwait(false);

            await account
                .RecordPasskeyUseAsync(stored.CredentialId, counter, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is Fido2VerificationException or JsonException or ArgumentException)
        {
            logger.LogInformation(
                ex,
                "Confirming a passkey for player {PlayerId} failed",
                identity.PlayerId
            );

            return AdminResults.Error(StatusCodes.Status400BadRequest, PASSKEY_FAILED);
        }

        var options = passkeys.BeginRegistration(
            identity.PlayerId,
            identity.Name,
            credentials.Select(x => x.CredentialId)
        );
        var ceremonyId = await grainFactory
            .GetAdminAuthGrain()
            .BeginCeremonyAsync(identity.PlayerId, AdminCeremonyKind.Register, options.ToJson(), ct)
            .ConfigureAwait(false);

        return AdminResults.Ceremony(ceremonyId, options.ToJson());
    }

    private async Task<IResult> AddPasskeyAsync(
        HttpContext http,
        AddPasskeyRequest request,
        CancellationToken ct
    )
    {
        var identity = AdminIdentity.Of(http);
        var ceremony = await grainFactory
            .GetAdminAuthGrain()
            .TakeCeremonyAsync(request.CeremonyId ?? string.Empty, AdminCeremonyKind.Register, ct)
            .ConfigureAwait(false);

        if (ceremony is null || ceremony.PlayerId != identity.PlayerId)
            return AdminResults.Error(StatusCodes.Status400BadRequest, PASSKEY_FAILED);

        try
        {
            var passkey = await passkeys
                .CompleteRegistrationAsync(
                    ceremony.OptionsJson,
                    request.Credential.Deserialize<AuthenticatorAttestationRawResponse>()!,
                    ct
                )
                .ConfigureAwait(false);

            await grainFactory
                .GetAdminAccountGrain(identity.PlayerId)
                .AddPasskeyAsync(passkey, request.Name ?? string.Empty, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is Fido2VerificationException or JsonException or ArgumentException)
        {
            logger.LogInformation(
                ex,
                "Adding a passkey for player {PlayerId} failed",
                identity.PlayerId
            );

            return AdminResults.Error(StatusCodes.Status400BadRequest, PASSKEY_FAILED);
        }

        return Results.NoContent();
    }

    private async Task<IResult> RemovePasskeyAsync(
        HttpContext http,
        int id,
        CancellationToken ct
    ) =>
        await grainFactory
            .GetAdminAccountGrain(AdminIdentity.Of(http).PlayerId)
            .RemovePasskeyAsync(id, ct)
            .ConfigureAwait(false)
            ? Results.NoContent()
            : AdminResults.Error(StatusCodes.Status409Conflict, LAST_PASSKEY);
}
