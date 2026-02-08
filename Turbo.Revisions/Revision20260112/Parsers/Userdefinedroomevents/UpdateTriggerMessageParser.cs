using System;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Parsers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260112.Parsers.Userdefinedroomevents;

internal class UpdateTriggerMessageParser : UpdateWiredDataParser, IParser
{
    public override Type UpdateMessageType => typeof(UpdateTriggerMessage);
}
