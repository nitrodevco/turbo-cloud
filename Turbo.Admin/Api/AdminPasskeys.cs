using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Options;
using Turbo.Admin.Configuration;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Api;

/// <summary>
/// The WebAuthn half of signing in: the options a browser needs to make or use a passkey, and
/// checking what it sends back. The checks themselves (challenge, origin, signature, counter)
/// are the Fido2 library's. Passkeys are bound to the panel's domain and accepted only from the
/// panel's own origin, so a look-alike site cannot use them.
/// </summary>
internal sealed class AdminPasskeys
{
    private const string RP_NAME = "Turbo Admin";

    private readonly Fido2 _fido2;
    private readonly Fido2Configuration _configuration;

    public AdminPasskeys(IOptions<AdminConfig> config)
    {
        var panel = new Uri(config.Value.PanelUrl);

        _configuration = new Fido2Configuration
        {
            RPID = string.IsNullOrWhiteSpace(config.Value.PasskeyRpId)
                ? panel.Host
                : config.Value.PasskeyRpId,
            RPName = RP_NAME,
            Origins = new HashSet<string> { panel.GetLeftPart(UriPartial.Authority) },
        };
        _fido2 = new Fido2(_configuration, null!);
    }

    /// <summary>
    /// What the browser needs to make a passkey for the player. The passkey is discoverable (the
    /// authenticator keeps the account with it), so signing in needs no name typed, and the
    /// authenticator must verify the person (biometrics or PIN), which makes the passkey on its
    /// own two factors: the device, and who unlocks it.
    /// </summary>
    public CredentialCreateOptions BeginRegistration(
        PlayerId playerId,
        string playerName,
        IEnumerable<byte[]> existingCredentialIds
    ) =>
        _fido2.RequestNewCredential(
            new RequestNewCredentialParams
            {
                User = new Fido2User
                {
                    Id = UserHandle(playerId),
                    Name = playerName,
                    DisplayName = playerName,
                },
                ExcludeCredentials =
                [
                    .. existingCredentialIds.Select(x => new PublicKeyCredentialDescriptor(x)),
                ],
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    ResidentKey = ResidentKeyRequirement.Required,
                    UserVerification = UserVerificationRequirement.Required,
                },
                AttestationPreference = AttestationConveyancePreference.None,
            }
        );

    /// <summary>Checks a new passkey against the options it was made for; throws when it fails.</summary>
    public async Task<AdminPasskeyCredential> CompleteRegistrationAsync(
        string optionsJson,
        AuthenticatorAttestationRawResponse response,
        CancellationToken ct
    )
    {
        var credential = await _fido2
            .MakeNewCredentialAsync(
                new MakeNewCredentialParams
                {
                    AttestationResponse = response,
                    OriginalOptions = CredentialCreateOptions.FromJson(optionsJson),
                    // The credential_id column is unique, so a reused id fails on save instead.
                    IsCredentialIdUniqueToUserCallback = static (_, _) => Task.FromResult(true),
                },
                ct
            )
            .ConfigureAwait(false);

        return new AdminPasskeyCredential
        {
            CredentialId = credential.Id,
            PublicKey = credential.PublicKey,
            SignCount = credential.SignCount,
            AaGuid = credential.AaGuid,
        };
    }

    /// <summary>
    /// What the browser needs to sign in with a passkey: one of these, or with none named, any
    /// passkey the device holds for the panel, which then says whose it is.
    /// </summary>
    public AssertionOptions BeginSignIn(IEnumerable<byte[]> credentialIds) =>
        _fido2.GetAssertionOptions(
            new GetAssertionOptionsParams
            {
                AllowedCredentials =
                [
                    .. credentialIds.Select(x => new PublicKeyCredentialDescriptor(x)),
                ],
                UserVerification = UserVerificationRequirement.Required,
            }
        );

    /// <summary>
    /// Checks a passkey answer against the options it was given and the stored public key, and
    /// returns the authenticator's new counter; throws when it fails.
    /// </summary>
    public async Task<uint> CompleteSignInAsync(
        string optionsJson,
        AuthenticatorAssertionRawResponse response,
        AdminPasskeyCredential stored,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var handle = UserHandle(playerId);
        var result = await _fido2
            .MakeAssertionAsync(
                new MakeAssertionParams
                {
                    AssertionResponse = response,
                    OriginalOptions = AssertionOptions.FromJson(optionsJson),
                    StoredPublicKey = stored.PublicKey,
                    StoredSignatureCounter = stored.SignCount,
                    IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                        Task.FromResult(
                            args.UserHandle is null || args.UserHandle.SequenceEqual(handle)
                        ),
                },
                ct
            )
            .ConfigureAwait(false);

        return result.SignCount;
    }

    /// <summary>The player a passkey's user handle names; null when it names none.</summary>
    public static PlayerId? PlayerOf(byte[]? userHandle) =>
        userHandle is { Length: sizeof(int) }
        && BinaryPrimitives.ReadInt32BigEndian(userHandle) is > 0 and var id
            ? new PlayerId(id)
            : null;

    /// <summary>
    /// The WebAuthn user handle: the player id, which is stable and says nothing about the player
    /// beyond what the hotel already shows.
    /// </summary>
    private static byte[] UserHandle(PlayerId playerId)
    {
        var handle = new byte[sizeof(int)];

        BinaryPrimitives.WriteInt32BigEndian(handle, playerId.Value);

        return handle;
    }
}
