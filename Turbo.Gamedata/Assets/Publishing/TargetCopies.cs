using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>
/// The copies of files read from publish targets to compare them, by target, path and the hash
/// recorded as sent there: read once, and again only once a publish sends the file (its recorded
/// hash changes). Up to <see cref="MAX_BYTES"/> in all, the copy read longest ago dropped first.
/// </summary>
internal sealed class TargetCopies
{
    public const long MAX_BYTES = 64L * 1024 * 1024;

    /// <summary>The largest copy read: a bundle bigger than this is not read to compare.</summary>
    public const int MAX_COPY_BYTES = 32 * 1024 * 1024;

    private readonly Lock _lock = new();
    private readonly Dictionary<(int Target, string Path, string Hash), byte[]> _copies = [];
    private readonly LinkedList<(int Target, string Path, string Hash)> _order = [];
    private long _bytes;

    public bool TryGet(int target, string path, string hash, out byte[] copy)
    {
        lock (_lock)
            return _copies.TryGetValue((target, path, hash), out copy!);
    }

    public void Put(int target, string path, string hash, byte[] copy)
    {
        if (copy.LongLength > MAX_BYTES)
            return;

        var key = (target, path, hash);

        lock (_lock)
        {
            if (_copies.Remove(key, out var old))
            {
                _order.Remove(key);
                _bytes -= old.LongLength;
            }

            while (_bytes + copy.LongLength > MAX_BYTES && _order.First is { } oldest)
            {
                _order.RemoveFirst();
                _bytes -= _copies[oldest.Value].LongLength;
                _copies.Remove(oldest.Value);
            }

            _copies[key] = copy;
            _order.AddLast(key);
            _bytes += copy.LongLength;
        }
    }

    /// <summary>A buffer a copy is read into, refusing one larger than <see cref="MAX_COPY_BYTES"/>.</summary>
    public sealed class Buffer : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            Check(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Check(buffer.Length);
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            Check(1);
            base.WriteByte(value);
        }

        private void Check(int count)
        {
            if (Length + count > MAX_COPY_BYTES)
                throw new IOException(
                    $"The target's copy is larger than the {MAX_COPY_BYTES / (1024 * 1024)} MB compared."
                );
        }
    }
}
