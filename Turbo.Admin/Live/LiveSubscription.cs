using System;
using System.Threading.Channels;
using Turbo.Admin.Api.Contracts;

namespace Turbo.Admin.Live;

/// <summary>
/// One open live stream's messages, from <see cref="AdminLiveFeed.Subscribe"/>. The reader
/// completes when the feed ends the stream for falling behind; disposing stops it.
/// </summary>
internal sealed class LiveSubscription(ChannelReader<LiveChangesMessage> reader, Action stop)
    : IDisposable
{
    public ChannelReader<LiveChangesMessage> Reader { get; } = reader;

    public void Dispose() => stop();
}
