using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SuperSocket.Connection;
using SuperSocket.ProtoBase;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Connections;
using SuperSocket.WebSocket.Server;
using Turbo.Networking.Connection;
using Turbo.Networking.Extensions;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// A connection reports its close, which is what removes its session and takes its player
/// offline, even when shutting its socket down throws: as it does for a client that reset the
/// connection while the server was still sending to it (a reload or a closed tab).
/// </summary>
public sealed class ClosingConnectionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AConnectionWhoseSocketCannotShutDown_StillReportsItsClose()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, Ct);
        var socket = await listener.AcceptSocketAsync(Ct);

        var factory = new ClosingConnectionFactoryBuilder(
            new SocketOptionsSetter(_ => { }),
            null!
        ).Build(
            new ListenOptions { Ip = "127.0.0.1", Port = port },
            new ConnectionOptions { Logger = NullLogger.Instance }
        );
        var connection = await factory.CreateConnection(socket, Ct);
        var closed = new TaskCompletionSource<CloseReason>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        connection.Closed += (_, e) => closed.TrySetResult(e.Reason);
        var reading = Task.Run(
            async () =>
            {
                await foreach (var _ in connection.RunAsync(new LinePipelineFilter())) { }
            },
            Ct
        );

        // The socket is gone before the connection shuts it down, so the shutdown throws.
        socket.Dispose();

        var reported = await Task.WhenAny(closed.Task, Task.Delay(TimeSpan.FromSeconds(5), Ct));

        reported.Should().BeSameAs(closed.Task, "the connection says it closed");
        connection.IsClosed.Should().BeTrue();
        await reading.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    // A receive buffer set by hand stops Linux sizing it and caps the TCP window, which made a
    // 256 KiB packet take seconds to arrive; the other socket options still apply.
    [Fact]
    public async Task AConnection_LeavesItsSocketsReceiveBufferToTheSystem()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, Ct);
        using var socket = await listener.AcceptSocketAsync(Ct);
        var systemSize = socket.ReceiveBufferSize;
        var options = new ConnectionOptions { Logger = NullLogger.Instance, SendTimeout = 1234 };

        var factory = new ClosingConnectionFactoryBuilder(
            new SocketOptionsSetter(_ => { }),
            null!
        ).Build(
            new ListenOptions
            {
                Ip = "127.0.0.1",
                Port = port,
                NoDelay = true,
            },
            options
        );
        await factory.CreateConnection(socket, Ct);

        options.ReceiveBufferSize.Should().BeGreaterThan(0).And.NotBe(systemSize);
        socket.ReceiveBufferSize.Should().Be(systemSize);
        socket.NoDelay.Should().BeTrue();
        socket.SendTimeout.Should().Be(1234);
    }

    [Fact]
    public void AHostWithClosingConnections_BuildsItsConnectionsThatWay()
    {
        using var host = WebSocketHostBuilder.Create().UseClosingConnections().Build();

        host.Services.GetRequiredService<IConnectionFactoryBuilder>()
            .Should()
            .BeOfType<ClosingConnectionFactoryBuilder>();
    }
}
