namespace Turbo.Crypto;

public sealed class Rc4Service(byte[] key)
{
    private readonly Rc4Engine _rc4Engine = new(key);

    public byte[] Process(byte[] inputData, byte[]? outputData = null, int? inputOffset = 0)
    {
        outputData ??= new byte[inputData.Length];

        _rc4Engine.ProcessBytes(inputData, 0, inputData.Length, outputData, inputOffset ?? 0);

        return outputData;
    }
}
