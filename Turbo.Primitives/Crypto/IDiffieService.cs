namespace Turbo.Primitives.Crypto;

public interface IDiffieService
{
    public byte[] GenerateSharedKey(string publicKey);

    //public BigInteger DecryptBigInteger(string str);
    public string GetSignedPrime();
    public string GetSignedGenerator();

    //public string EncryptBigInteger(BigInteger integer);
    public byte[] GetSharedKey(string publicKeyStr);
    public string GetPublicKey();
}
