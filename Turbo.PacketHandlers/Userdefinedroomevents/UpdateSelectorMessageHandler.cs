using Orleans;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateSelectorMessageHandler(IGrainFactory grainFactory)
    : UpdateWiredMessageHandler<UpdateSelectorMessage>(grainFactory)
{
    // Official client capture 2026-10-24: a 21-pick save (wired matrix selector.1.E2).
    protected override int ErrorReportMessageId => 3058;
}
