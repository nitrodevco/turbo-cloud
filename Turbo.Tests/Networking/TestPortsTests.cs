using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// The test servers (admin, gamedata, public site, Web API) bind the port they were given later:
/// a port that an outgoing connection or another test can take in between failed their start.
/// </summary>
public sealed class TestPortsTests
{
    [Fact]
    public void APort_IsBelowTheDynamicRange_WhereClientConnectionsTakeTheirs()
    {
        var port = TestPorts.Free();

        port.Should().BeInRange(TestPorts.First, TestPorts.Last).And.BeLessThan(32768);
    }

    [Fact]
    public void TestsAskingAtOnce_NeverGetTheSamePort()
    {
        var ports = new int[200];

        Parallel.For(0, ports.Length, i => ports[i] = TestPorts.Free());

        ports.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void APortSomethingListensOn_IsSkipped()
    {
        var taken = new List<TcpListener>();

        try
        {
            // Every one of the next ports the helper would try is taken: it must hand out another.
            var next = TestPorts.Free() + 1;

            for (var port = next; port < next + 4 && port <= TestPorts.Last; port++)
            {
                var listener = new TcpListener(IPAddress.Loopback, port);

                // Another test or test run already listening on it takes it all the same.
                try
                {
                    listener.Start();
                    taken.Add(listener);
                }
                catch (SocketException)
                {
                    listener.Dispose();
                }
            }

            var free = TestPorts.Free();

            taken.Select(l => ((IPEndPoint)l.LocalEndpoint).Port).Should().NotContain(free);
            using var server = new TcpListener(IPAddress.Loopback, free);
            server.Invoking(s => s.Start()).Should().NotThrow();
        }
        finally
        {
            taken.ForEach(l => l.Dispose());
        }
    }
}
