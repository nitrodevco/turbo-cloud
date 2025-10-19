using System;

namespace Turbo.Crypto;

public sealed class Rc4Engine
{
    private readonly byte[] _data = new byte[256];
    private int _i;
    private int _j;
    private byte[]? _workingKey;
    private readonly int _dropN;
    private readonly object _sync = new();

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

        lock (_sync)
        {
            for (int k = 0; k < length; k++)
            {
                outputData[outputOffset + k] = (byte)(
                    inputData[inputOffset + k] ^ NextKeyStreamByte_NoLock()
                );
            }
        }

        return outputData;
    }

    public byte[] Peek(byte[] inputData, int inputOffset = 0, int? length = null)
    {
        ArgumentNullException.ThrowIfNull(inputData);
        int len = length ?? (inputData.Length - inputOffset);
        if (inputOffset < 0 || len < 0)
            throw new ArgumentOutOfRangeException();
        if (inputData.Length - inputOffset < len)
            throw new ArgumentException("Input buffer too short.");

        var output = new byte[len];
        Peek(inputData, inputOffset, output, 0, len);
        return output;
    }

    public void Peek(
        byte[] inputData,
        int inputOffset,
        byte[] outputData,
        int outputOffset,
        int length
    )
    {
        ArgumentNullException.ThrowIfNull(inputData);
        ArgumentNullException.ThrowIfNull(outputData);
        if (inputOffset < 0 || outputOffset < 0 || length < 0)
            throw new ArgumentOutOfRangeException();
        if (inputData.Length - inputOffset < length)
            throw new ArgumentException("Input buffer too short.");
        if (outputData.Length - outputOffset < length)
            throw new ArgumentException("Output buffer too short.");

        byte[] sClone = new byte[256];
        int ic,
            jc;
        lock (_sync)
        {
            Buffer.BlockCopy(_data, 0, sClone, 0, 256);
            ic = _i;
            jc = _j;
        }

        for (int k = 0; k < length; k++)
        {
            ic = (ic + 1) & 0xFF;
            jc = (jc + sClone[ic]) & 0xFF;
            Swap(sClone, ic, jc);
            int t = (sClone[ic] + sClone[jc]) & 0xFF;
            byte ks = sClone[t];

            outputData[outputOffset + k] = (byte)(inputData[inputOffset + k] ^ ks);
        }
    }

    private void SetKey(byte[] key)
    {
        lock (_sync)
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
                _ = NextKeyStreamByte_NoLock();
        }
    }

    private byte NextKeyStreamByte_NoLock()
    {
        _i = (_i + 1) & 0xFF;
        _j = (_j + _data[_i]) & 0xFF;
        Swap(_data, _i, _j);
        int t = (_data[_i] + _data[_j]) & 0xFF;
        return _data[t];
    }

    private static void Swap(byte[] s, int a, int b) => (s[b], s[a]) = (s[a], s[b]);
}
