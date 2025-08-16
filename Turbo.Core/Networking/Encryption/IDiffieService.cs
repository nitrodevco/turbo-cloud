using Org.BouncyCastle.Math;

namespace Turbo.Core.Networking.Encryption;

public interface IDiffieService
{
    byte[] GenerateSharedKey(string publicKey);

    BigInteger DecryptBigInteger(string str);

    string GetSignedPrime();

    string GetSignedGenerator();

    string EncryptBigInteger(BigInteger integer);

    string GetPublicKey();

    byte[] GetSharedKey(string messageSharedKey);
}
