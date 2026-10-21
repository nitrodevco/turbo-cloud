using System;
using System.Text;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;
using Turbo.Primitives.Crypto;

namespace Turbo.Crypto;

public sealed class DiffieService : IDiffieService
{
    private const int DH_PRIMES_BIT_SIZE = 128;
    private readonly BigInteger _dhGenerator;
    private readonly BigInteger _dhPrime;
    private readonly BigInteger _dhPrivate;
    private readonly BigInteger _dhPublic;
    private readonly IRsaService _rsaService;

    // What the server signs for the client never changes while it runs, and PKCS#1 v1.5 signing
    // is deterministic, so each is signed once, on first use, not by every handshake.
    private readonly Lazy<string> _signedPrime;
    private readonly Lazy<string> _signedGenerator;
    private readonly Lazy<string> _signedPublicKey;

    public DiffieService(IRsaService rsaService)
    {
        var random = new SecureRandom();
        _rsaService = rsaService;

        // Generate probable primes for DH parameters
        _dhPrime = BigInteger.ProbablePrime(DH_PRIMES_BIT_SIZE, random);
        _dhGenerator = BigInteger.ProbablePrime(DH_PRIMES_BIT_SIZE, random);

        while (_dhGenerator.CompareTo(_dhPrime) <= 0)
        {
            _dhPrime = BigInteger.ProbablePrime(DH_PRIMES_BIT_SIZE, random);
            _dhGenerator = BigInteger.ProbablePrime(DH_PRIMES_BIT_SIZE, random);
        }

        (_dhPrime, _dhGenerator) = (_dhGenerator, _dhPrime);

        _dhPrivate = BigInteger.ProbablePrime(DH_PRIMES_BIT_SIZE, random);
        _dhPublic = _dhGenerator.ModPow(_dhPrivate, _dhPrime);

        _signedPrime = new(() => EncryptBigInteger(_dhPrime));
        _signedGenerator = new(() => EncryptBigInteger(_dhGenerator));
        _signedPublicKey = new(() => EncryptBigInteger(_dhPublic));
    }

    public byte[] GenerateSharedKey(string publicKey)
    {
        var pubKey = DecryptBigInteger(publicKey);
        var sharedKey = pubKey.ModPow(_dhPrivate, _dhPrime);

        return sharedKey.ToByteArrayUnsigned();
    }

    public BigInteger DecryptBigInteger(string str)
    {
        var bytes = Hex.Decode(str);
        var decrypted = _rsaService.Decrypt(bytes);
        var intStr = Encoding.UTF8.GetString(decrypted);

        return new BigInteger(intStr);
    }

    public string GetSignedPrime() => _signedPrime.Value;

    public string GetSignedGenerator() => _signedGenerator.Value;

    public string EncryptBigInteger(BigInteger integer)
    {
        var str = integer.ToString(10);
        var bytes = Encoding.UTF8.GetBytes(str);

        // Use RSA to sign the byte array
        var encrypted = _rsaService.Sign(bytes);

        return Hex.ToHexString(encrypted).ToLowerInvariant();
    }

    public byte[] GetSharedKey(string publicKeyStr)
    {
        var publicKey = DecryptBigInteger(publicKeyStr);
        var sharedKey = publicKey.ModPow(_dhPrivate, _dhPrime);

        return sharedKey.ToByteArrayUnsigned();
    }

    public string GetPublicKey() => _signedPublicKey.Value;
}
