using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Plugins.Messages;

namespace Skills;

// What the plugin's client and server halves say to each other: named messages (turbo-cloud
// docs/plugin-messages.md). The server gives each a header as the plugin loads and sends clients the
// names and fields; the client module reads and sends them by name (client/src/skill.ts). A name
// starts with the plugin's key; the fields are the record's, in order, camelCase on the client.

/// <summary>Client to server: the window wants the player's skill.</summary>
[PluginMessage("skills.get")]
public sealed record GetSkillMessage() : IPluginMessage;

/// <summary>Client to server: the window's Train button.</summary>
[PluginMessage("skills.train")]
public sealed record TrainMessage() : IPluginMessage;

/// <summary>Server to client: level, experience in the level, experience the level needs.</summary>
[PluginMessage("skills.state")]
public sealed record SkillMessage(int Level, int Experience, int Needed) : IPluginMessage
{
    public static SkillMessage Of(Skill skill) => new(skill.Level, skill.Experience, skill.Needed);
}

/// <summary>Found as the plugin loads, like every handler in the assembly.</summary>
public sealed class GetSkillHandler(SkillBook book) : IPluginMessageHandler<GetSkillMessage>
{
    public async ValueTask HandleAsync(
        GetSkillMessage message,
        PluginMessageContext ctx,
        CancellationToken ct
    ) => await ctx.ReplyAsync(SkillMessage.Of(book.Of(ctx.PlayerId)), ct).ConfigureAwait(false);
}

public sealed class TrainHandler(SkillBook book) : IPluginMessageHandler<TrainMessage>
{
    public async ValueTask HandleAsync(
        TrainMessage message,
        PluginMessageContext ctx,
        CancellationToken ct
    ) => await ctx.ReplyAsync(SkillMessage.Of(book.Train(ctx.PlayerId)), ct).ConfigureAwait(false);
}
