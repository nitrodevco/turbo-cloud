using System.Linq;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// One outgoing header is one message to the client, so the revision has one composer for it. A
/// second composer on the same header is a second way to send it - FigureSetIds had an empty one
/// beside the real one, which would have told the client the player owns no clothing at all.
/// </summary>
public class RevisionSerializerHeaderTests
{
    [Fact]
    public void EveryOutgoingHeader_HasOneComposer()
    {
        var shared = PacketHarness
            .Revision.Serializers.GroupBy(x => x.Value.Header)
            .Where(x => x.Count() > 1)
            .Select(x => $"{x.Key}: {string.Join(", ", x.Select(y => y.Key.Name).Order())}")
            .ToList();

        Assert.Empty(shared);
    }
}
