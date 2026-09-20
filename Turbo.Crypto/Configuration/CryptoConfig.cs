namespace Turbo.Crypto.Configuration;

/// <summary>
/// The RSA key pair the handshake is built on. These are the one kind of option that is
/// <c>required</c>: there is no sensible default for a hotel's keys, so they carry none and have
/// to be in <c>appsettings.json</c>.
/// </summary>
public class CryptoConfig
{
    public const string SECTION_NAME = "Turbo:Crypto";

    public required string KeySize { get; init; }
    public required string PublicKey { get; init; }
    public required string PrivateKey { get; init; }
    public required bool EnableServerToClientEncryption { get; init; }
}
