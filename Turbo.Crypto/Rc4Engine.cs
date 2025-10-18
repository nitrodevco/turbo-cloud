using System;

namespace Turbo.Crypto;

public sealed class Rc4Engine
{
    private readonly byte[] _data = new byte[256];
    private int _i;
    private int _j;
    private byte[]? _workingKey;
    private readonly int _dropN;

    public Rc4Engine(byte[] key, int dropN = 0)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentOutOfRangeException.ThrowIfNegative(dropN);

        if (key.Length == 0 || key.Length > 256)
            throw new ArgumentException("Key length must be 1..256 bytes for RC4.", nameof(key));

        _workingKey = new byte[key.Length];
        _dropN = dropN;

        Buffer.BlockCopy(key, 0, _workingKey, 0, key.Length);

        SetKey(_workingKey);
    }

    public byte[] Process(byte[] inputData, byte[]? outputData = null, int? inputOffset = 0)
    {
        outputData ??= new byte[inputData.Length];

        return ProcessBytes(inputData, 0, inputData.Length, outputData, inputOffset ?? 0);
    }

    public byte[] ProcessBytes(
        byte[] inputData,
        int inputOffset,
        int length,
        byte[] outputData,
        int outputOffset
    )
    {
        ArgumentNullException.ThrowIfNull(inputData);
        ArgumentNullException.ThrowIfNull(outputData);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (inputOffset < 0 || outputOffset < 0)
            throw new ArgumentOutOfRangeException("Offsets must be non-negative.");

        if (inputData.Length - inputOffset < length)
            throw new ArgumentException("Input buffer too short.");

        if (outputData.Length - outputOffset < length)
            throw new ArgumentException("Output buffer too short.");

        for (int k = 0; k < length; k++)
        {
            outputData[outputOffset + k] = (byte)(inputData[inputOffset + k] ^ NextKeyStreamByte());
        }

        return outputData;
    }

    private void SetKey(byte[] key)
    {
        for (int v = 0; v < 256; v++)
            _data[v] = (byte)v;

        _i = 0;
        _j = 0;

        int j = 0;
        for (int i = 0; i < 256; i++)
        {
            j = (j + _data[i] + key[i % key.Length]) & 0xFF;

            Swap(_data, i, j);
        }

        for (int n = 0; n < _dropN; n++)
            _ = NextKeyStreamByte();
    }

    private byte NextKeyStreamByte()
    {
        _i = (_i + 1) & 0xFF;
        _j = (_j + _data[_i]) & 0xFF;

        Swap(_data, _i, _j);

        int t = (_data[_i] + _data[_j]) & 0xFF;

        return _data[t];
    }

    private static void Swap(byte[] s, int a, int b) => (s[b], s[a]) = (s[a], s[b]);
}
