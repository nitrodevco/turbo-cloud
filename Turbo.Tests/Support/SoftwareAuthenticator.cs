using System.Buffers.Binary;
using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Turbo.Tests.Support;

/// <summary>
/// A passkey authenticator in software, for testing the admin panel's WebAuthn checks without a
/// browser: it makes a P-256 key pair, answers a registration with a "none" attestation and a
/// sign-in with a DER signature, exactly as a platform authenticator does, and produces the JSON
/// a browser's <c>PublicKeyCredential.toJSON()</c> would.
/// </summary>
public sealed class SoftwareAuthenticator(string origin) : IDisposable
{
    private const byte USER_PRESENT = 0x01;
    private const byte USER_VERIFIED = 0x04;
    private const byte ATTESTED_DATA = 0x40;

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private uint _counter;

    public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(16);

    /// <summary>Who the authenticator says it was verifying; a real one never says less.</summary>
    public bool VerifiesUser { get; set; } = true;

    public void Dispose() => _key.Dispose();

    /// <summary>The answer to <c>navigator.credentials.create</c> for these options.</summary>
    public JsonElement Register(string optionsJson)
    {
        var options = JsonNode.Parse(optionsJson)!;
        var rpId = options["rp"]!["id"]!.GetValue<string>();
        var clientData = ClientData("webauthn.create", options["challenge"]!.GetValue<string>());

        var parameters = _key.ExportParameters(includePrivateParameters: false);
        var cose = new CborWriter(CborConformanceMode.Ctap2Canonical);
        cose.WriteStartMap(5);
        cose.WriteInt32(1); // kty: EC2
        cose.WriteInt32(2);
        cose.WriteInt32(3); // alg: ES256
        cose.WriteInt32(-7);
        cose.WriteInt32(-1); // crv: P-256
        cose.WriteInt32(1);
        cose.WriteInt32(-2); // x
        cose.WriteByteString(parameters.Q.X!);
        cose.WriteInt32(-3); // y
        cose.WriteByteString(parameters.Q.Y!);
        cose.WriteEndMap();

        var authData = new List<byte>();
        authData.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(rpId)));
        authData.Add((byte)(USER_PRESENT | (VerifiesUser ? USER_VERIFIED : 0) | ATTESTED_DATA));
        authData.AddRange(BigEndian(_counter));
        authData.AddRange(new byte[16]); // AAGUID: none
        authData.AddRange([(byte)(CredentialId.Length >> 8), (byte)CredentialId.Length]);
        authData.AddRange(CredentialId);
        authData.AddRange(cose.Encode());

        var attestation = new CborWriter(CborConformanceMode.Ctap2Canonical);
        attestation.WriteStartMap(3);
        attestation.WriteTextString("fmt");
        attestation.WriteTextString("none");
        attestation.WriteTextString("attStmt");
        attestation.WriteStartMap(0);
        attestation.WriteEndMap();
        attestation.WriteTextString("authData");
        attestation.WriteByteString([.. authData]);
        attestation.WriteEndMap();

        return Credential(
            new JsonObject
            {
                ["clientDataJSON"] = B64(clientData),
                ["attestationObject"] = B64(attestation.Encode()),
                ["transports"] = new JsonArray("internal"),
            }
        );
    }

    /// <summary>The answer to <c>navigator.credentials.get</c> for these options.</summary>
    public JsonElement SignIn(string optionsJson, byte[] userHandle, string? asOrigin = null)
    {
        var options = JsonNode.Parse(optionsJson)!;
        var rpId = options["rpId"]!.GetValue<string>();
        var clientData = ClientData(
            "webauthn.get",
            options["challenge"]!.GetValue<string>(),
            asOrigin
        );

        _counter++;

        var authData = new List<byte>();
        authData.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(rpId)));
        authData.Add((byte)(USER_PRESENT | (VerifiesUser ? USER_VERIFIED : 0)));
        authData.AddRange(BigEndian(_counter));

        byte[] authenticatorData = [.. authData];
        var signature = _key.SignData(
            [.. authenticatorData, .. SHA256.HashData(clientData)],
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence
        );

        return Credential(
            new JsonObject
            {
                ["clientDataJSON"] = B64(clientData),
                ["authenticatorData"] = B64(authenticatorData),
                ["signature"] = B64(signature),
                ["userHandle"] = B64(userHandle),
            }
        );
    }

    private byte[] ClientData(string type, string challenge, string? asOrigin = null) =>
        Encoding.UTF8.GetBytes(
            new JsonObject
            {
                ["type"] = type,
                ["challenge"] = challenge,
                ["origin"] = asOrigin ?? origin,
                ["crossOrigin"] = false,
            }.ToJsonString()
        );

    private JsonElement Credential(JsonObject response) =>
        JsonSerializer.SerializeToElement(
            new JsonObject
            {
                ["id"] = B64(CredentialId),
                ["rawId"] = B64(CredentialId),
                ["type"] = "public-key",
                ["response"] = response,
                ["clientExtensionResults"] = new JsonObject(),
            }
        );

    private static byte[] BigEndian(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);

        return bytes;
    }

    private static string B64(byte[] bytes) => Base64Url.EncodeToString(bytes);
}
