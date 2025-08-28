using System.Buffers;
using System.Threading.Tasks;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;

namespace Turbo.Core.Networking.Protocol;

public delegate void FrameStage(
    ref SequenceReader<byte> reader,
    ISessionContext ctx,
    ref IClientPacket? clientPacket
);
