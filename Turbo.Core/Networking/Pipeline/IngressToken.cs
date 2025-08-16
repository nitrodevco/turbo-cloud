using DotNetty.Transport.Channels;

namespace Turbo.Core.Networking.Pipeline;

public record IngressToken(IChannelId ChannelId);
