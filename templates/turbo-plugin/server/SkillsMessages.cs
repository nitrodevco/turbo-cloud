using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Packets;

namespace Skills;

/// <summary>
/// What the plugin's client and server halves say to each other. Headers come from the extension
/// range (20000-32767, outside the server's own 30000-30099); the client module uses the same
/// numbers (<c>client/src/skill.ts</c>). Pick numbers no other installed plugin uses.
/// TODO: named plugin messages, once the server has them, replace the numbers and the serializer.
/// </summary>
public static class SkillsHeaders
{
    /// <summary>Client to server: the window wants the player's skill.</summary>
    public const int GET_SKILL = 20400;

    /// <summary>Server to client: level, experience in the level, experience the level needs.</summary>
    public const int SKILL = 20401;

    /// <summary>Client to server: the window's Train button.</summary>
    public const int TRAIN = 20402;
}

public sealed record GetSkillMessage : IMessageEvent;

public sealed record TrainMessage : IMessageEvent;

/// <summary>Sent only to clients that run client modules: a client without the module would not read it.</summary>
public sealed record SkillMessage(int Level, int Experience, int Needed) : ICapabilityComposer
{
    public string Capability => ClientCapabilities.CLIENT_MODULES;

    public static SkillMessage Of(Skill skill) => new(skill.Level, skill.Experience, skill.Needed);
}

internal sealed class EmptyParser(Func<IMessageEvent> create) : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => create();
}

internal sealed class SkillSerializer() : AbstractSerializer<SkillMessage>(SkillsHeaders.SKILL)
{
    protected override void Serialize(IServerPacket packet, SkillMessage message) =>
        packet
            .WriteInteger(message.Level)
            .WriteInteger(message.Experience)
            .WriteInteger(message.Needed);
}

/// <summary>Found as the plugin loads, like every handler in the assembly.</summary>
public sealed class GetSkillHandler(SkillBook book) : IMessageHandler<GetSkillMessage>
{
    public async ValueTask HandleAsync(
        GetSkillMessage message,
        MessageContext ctx,
        CancellationToken ct
    ) =>
        await ctx.SendComposerAsync(SkillMessage.Of(book.Of(ctx.PlayerId)), ct)
            .ConfigureAwait(false);
}

public sealed class TrainHandler(SkillBook book) : IMessageHandler<TrainMessage>
{
    public async ValueTask HandleAsync(
        TrainMessage message,
        MessageContext ctx,
        CancellationToken ct
    ) =>
        await ctx.SendComposerAsync(SkillMessage.Of(book.Train(ctx.PlayerId)), ct)
            .ConfigureAwait(false);
}
