using Orleans;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateAddonMessageHandler(IGrainFactory grainFactory)
    : UpdateWiredMessageHandler<UpdateAddonMessage>(grainFactory)
{
    // Official client capture 2026-10-24: a 21-pick save (wired matrix addon.19.E2).
    protected override int ErrorReportMessageId => 3055;
}
