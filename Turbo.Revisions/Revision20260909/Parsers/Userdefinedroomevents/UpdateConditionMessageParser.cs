using System;
using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents;

/// <summary>
/// A condition save carries one definition specific, the quantifier the player picked, and no
/// type specifics. The quantifier type and the invert flag travel the other way only: the
/// server declares them, the client draws them and never sends them back.
/// </summary>
internal class UpdateConditionMessageParser : UpdateWiredDataParser, IParser
{
    public override List<object> GetRequiredDefinitionSpecifics() => [1];

    public override Type UpdateMessageType => typeof(UpdateConditionMessage);
}
