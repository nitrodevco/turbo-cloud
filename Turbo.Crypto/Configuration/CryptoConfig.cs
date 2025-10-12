namespace Turbo.Crypto.Configuration;

public class CryptoConfig
{
    public const string SECTION_NAME = "Turbo:Crypto";

    public required string KeySize { get; init; }
    public required string PublicKey { get; init; }
    public required string PrivateKey { get; init; }
    public required bool EnableServerToClientEncryption { get; init; }
}
