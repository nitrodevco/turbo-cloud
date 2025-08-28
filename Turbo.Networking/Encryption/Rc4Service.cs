using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Turbo.Core.Networking.Encryption;

namespace Turbo.Networking.Encryption;

public class Rc4Service : IRc4Service
{
    private readonly RC4Engine _rc4Engine = new();

    public Rc4Service(byte[] key)
    {
        _rc4Engine.Init(true, new KeyParameter(key));
    }

    public byte[] ProcessBytes(byte[] bytes)
    {
        var outputData = new byte[bytes.Length];

        _rc4Engine.ProcessBytes(bytes, 0, bytes.Length, outputData, 0);

        return outputData;
    }
}
