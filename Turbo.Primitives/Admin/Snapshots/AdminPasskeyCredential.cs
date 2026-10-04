using System;
using Orleans;

namespace Turbo.Primitives.Admin.Snapshots;

/// <summary>
/// The public half of a passkey, what checking a signature with it needs: never secret, since
/// the private key never leaves the player's authenticator.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AdminPasskeyCredential
{
    [Id(0)]
    public required byte[] CredentialId { get; init; }

    [Id(1)]
    public required byte[] PublicKey { get; init; }

    [Id(2)]
    public required uint SignCount { get; init; }

    [Id(3)]
    public Guid AaGuid { get; init; }
}
