using DotNetty.Buffers;

namespace Turbo.Networking.Extensions;

public static class ByteBufferExtensions
{
    public static bool ReleaseAll(this IByteBuffer buffer)
    {
        var refCount = buffer.ReferenceCount;
        return refCount > 0 && buffer.Release(refCount);
    }
}
