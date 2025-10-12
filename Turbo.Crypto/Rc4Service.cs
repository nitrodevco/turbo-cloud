using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace Turbo.Crypto;

public sealed class Rc4Service
{
    private readonly RC4Engine _rc4Engine = new();

    public Rc4Service(byte[] key)
    {
        _rc4Engine.Init(true, new KeyParameter(key));
    }

    public byte[] Process(byte[] inputData, byte[]? outputData = null, int? inputOffset = 0)
    {
        outputData ??= new byte[inputData.Length];

        _rc4Engine.ProcessBytes(inputData, 0, inputData.Length, outputData, inputOffset ?? 0);

        return outputData;
    }
}
