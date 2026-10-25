/**
 * `npm run make <kind> <name> [options]`: adds one piece to the plugin, in the files and the shape
 * the server and the client find by themselves, and prints the guide to read next. Nothing is
 * registered by hand: the server finds each class as the plugin loads, and client/src/made/index.ts
 * (rewritten here) activates each client piece with the module.
 *
 *   command <name>                      :<name>, with its permission node (server/Commands)
 *   message <name> [--to client|server] [--fields level:int,name:string]
 *                                       a named message (server/Messages, client/src/made); --to
 *                                       server (default) adds its handler, --to client a listener
 *   logic <key>                         furni logic for the furniture whose logic is <key> (server/Logic)
 *   wired <effect|trigger|condition|selector> <name>
 *                                       a wired box of the plugin's own (server/Wired); a trigger
 *                                       comes with its event, fired by hotel.Wired.FireAsync
 *   listener <name>                     a room event listener (server/Listeners)
 *   setting <name> [--type int|long|bool|string|double] [--default <value>]
 *                                       a setting in the plugin's [PluginSettings] class
 *   route <path> [--method GET] [--anonymous]
 *                                       a web route at /api/plugins/<key>/<path> (server/Routes)
 *   window <name>                       a window, opened by the link <key>/<name> (client/src/made)
 *   patch <library>/<asset>             a template patch on a core window (wwwroot/template-patches.json)
 *   migration <Name>                    the plugin's tables (server/Database) and a migration (dotnet ef)
 *   schedule <name> [--cron "0 18 * * MON" | --every 00:05:00] [--per-server]
 *                                       a scheduled task, run once per cluster (server/Tasks)
 *   policy <purchase|room-entry|placement|registration> [<name>]
 *                                       a game policy: allow, refuse or change the action (server/Policies)
 *
 * A file that is there already is left alone (the command says so and stops).
 */
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { buildErrors, checkHost, turboBin } from './host.mjs';

const root = path.resolve(import.meta.dirname, '..');
const manifestFile = path.join(root, 'manifest.json');
const manifest = JSON.parse(readFileSync(manifestFile, 'utf8'));
const key = manifest.Key;
const assembly = path.parse(manifest.AssemblyFile).name;
const server = path.join(root, 'server');
const made = path.join(root, 'client', 'src', 'made');
const DOCS = 'turbo-cloud docs/';

const [ kind, name, ...rest ] = process.argv.slice(2);

const fail = (text) => {
    console.error(text);
    process.exit(1);
};

const option = (flag, fallback) => {
    const at = rest.indexOf(`--${flag}`);

    if (at < 0) return fallback;

    return (rest[at + 1] && !rest[at + 1].startsWith('--')) ? rest[at + 1] : true;
};

/** `skill_stove`, `rares/values`, `skills.levelled` -> `SkillStove`, `RaresValues`, `Levelled` (after the key). */
const pascal = value => value
    .replace(new RegExp(`^${key}\\.`), '')
    .split(/[^A-Za-z0-9]+/)
    .filter(Boolean)
    .map(part => part[0].toUpperCase() + part.slice(1))
    .join('');

const camel = value => value[0].toLowerCase() + value.slice(1);

const constant = value => pascal(value).replace(/([a-z0-9])([A-Z])/g, '$1_$2').toUpperCase();

const written = [];

const write = (file, content) => {
    if (existsSync(file)) fail(`${path.relative(root, file)} is there already: nothing was written.`);

    mkdirSync(path.dirname(file), { recursive: true });
    writeFileSync(file, content.replace(/^\n/, ''));
    written.push(path.relative(root, file));
};

const change = (file, edit) => {
    const before = existsSync(file) ? readFileSync(file, 'utf8') : '';
    const after = edit(before);

    if (after === before) return;

    mkdirSync(path.dirname(file), { recursive: true });
    writeFileSync(file, after);

    if (!written.includes(path.relative(root, file))) written.push(path.relative(root, file));
};

/** A text the hotel gets as the plugin loads (staff can change it in the panel). */
const text = (textKey, value) => change(path.join(root, 'gamedata', 'external_texts.txt'), (texts) => {
    if (texts.split(/\r?\n/).some(line => line.startsWith(`${textKey}=`))) return texts;

    return `${texts}${(texts && !texts.endsWith('\n')) ? '\n' : ''}${textKey}=${value}\n`;
});

/** A reference to one of the server's assemblies (TurboBin), kept out of the plugin's folder. */
const reference = (dll, why) => change(path.join(server, `${assembly}.csproj`), (project) => {
    if (project.includes(`$(TurboBin)/${dll}"`)) return project;

    const comment = `    <!-- ${why} -->\n`;
    const line = `    <Reference Include="$(TurboBin)/${dll}" Private="$(TurboPrivate)" />\n`;
    const anchor = project.lastIndexOf('    <Reference Include="$(TurboBin)/');
    const end = project.indexOf('\n', anchor) + 1;

    return `${project.slice(0, end)}${project.includes(comment) ? '' : comment}${line}${project.slice(end)}`;
});

/** Rewrites client/src/made/index.ts to activate every made piece with an `activate`. */
const listMade = () => {
    const pieces = existsSync(made)
        ? readdirSync(made)
            .filter(file => /\.tsx?$/.test(file) && (file !== 'index.ts'))
            .filter(file => /export const activate\b/.test(readFileSync(path.join(made, file), 'utf8')))
            .sort()
        : [];
    const imports = pieces.map((file, i) => `import { activate as made${i} } from './${file.replace(/\.tsx?$/, '')}';`);

    change(path.join(made, 'index.ts'), () => `/**
 * What \`npm run make\` added to the client module, activated with it (client/src/index.tsx).
 * Rewritten by tools/make.mjs: every file here with an \`activate\` is listed.
 */
import type { ClientModuleApi } from '@nitrodevco/client-module-sdk';
${imports.length ? `\n${imports.join('\n')}\n` : ''}
const made: ((api: ClientModuleApi) => void)[] = [${pieces.length ? ` ${pieces.map((_, i) => `made${i}`).join(', ')} ` : ''}];

export const activateMade = (api: ClientModuleApi) => made.forEach(activate => activate(api));
`);
};

/** The EF Core version of the server the plugin builds against (TurboBin), from its .deps.json. */
const efVersion = () => {
    const bin = turboBin();
    const deps = existsSync(bin) ? readdirSync(bin).filter(file => file.endsWith('.deps.json')) : [];

    for (const file of deps) {
        const libraries = Object.keys(JSON.parse(readFileSync(path.join(bin, file), 'utf8')).libraries ?? {});
        const ef = libraries.find(library => library.startsWith('Microsoft.EntityFrameworkCore/'));

        if (ef) return ef.split('/')[1];
    }

    return fail(`No server build in ${bin} to read its EF Core version from: build the server, or set TurboBin to its bin folder.`);
};

const FIELD_TYPES = { int: 'int', long: 'long', bool: 'bool', string: 'string', double: 'double' };

const recipes = {
    command() {
        if (!/^[a-z0-9_]+$/.test(name)) fail('A command is lower case letters, digits and _: npm run make command hug');

        const type = `${pascal(name)}Command`;

        write(path.join(server, 'Commands', `${type}.cs`), `
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace ${assembly};

/// <summary><c>:${name}</c>. Found as the plugin loads.</summary>
[Command("${name}", Description = "${pascal(name)}")]
[RequiresPermission(${type}.PERMISSION)]
public sealed class ${type} : ICommand<${type}.Arguments>
{
    public const string PERMISSION = "${key}.command.${name}";

    public const string DONE = "done";

    /// <summary>What follows the command, in order (turbo-cloud docs/commands.md).</summary>
    public sealed record Arguments;

    /// <summary>Used while the hotel's texts have no <c>command.${name}.done</c>.</summary>
    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string> { [DONE] = "Done." };

    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        Arguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Done(DONE));
}

/// <summary>Who may use <c>:${name}</c>: a node a hotel grants or denies per rank.</summary>
public sealed class ${type}Permission : IPermissionNodeSource
{
    public string? Prefix => "${key}";

    public IEnumerable<PermissionNodeDefinition> Nodes =>
        [new(${type}.PERMISSION, "Use :${name}", GrantedByDefault: true)];

    public IEnumerable<PermissionMetaDefinition> MetaKeys => [];
}
`);

        return [ 'commands.md', 'permissions.md' ];
    },

    message() {
        const messageName = name.startsWith(`${key}.`) ? name : `${key}.${name}`;
        const toClient = option('to', 'server') === 'client';
        const fields = (option('fields', '') || '').split(',').filter(Boolean).map((field) => {
            const [ fieldName, type = 'int' ] = field.split(':');

            if (!FIELD_TYPES[type]) fail(`${type}: a field is one of ${Object.keys(FIELD_TYPES).join(', ')}`);

            return { name: fieldName, type };
        });
        const type = `${pascal(messageName)}Message`;
        const parameters = fields.map(field => `${FIELD_TYPES[field.type]} ${pascal(field.name)}`).join(', ');
        const handler = toClient ? '' : `
/// <summary>Found as the plugin loads; <c>ctx.ReplyAsync</c> answers the player who sent it.</summary>
public sealed class ${pascal(messageName)}Handler : IPluginMessageHandler<${type}>
{
    public ValueTask HandleAsync(
        ${type} message,
        PluginMessageContext ctx,
        CancellationToken ct
    ) => ValueTask.CompletedTask;
}
`;

        write(path.join(server, 'Messages', `${type}.cs`), `
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Plugins.Messages;

namespace ${assembly};

/// <summary>${toClient ? 'Server to client' : 'Client to server'}: <c>${messageName}</c> (client/src/made/${messageName}.ts).</summary>
[PluginMessage("${messageName}")]
public sealed record ${type}(${parameters}) : IPluginMessage;
${handler}`);

        const NAME = constant(messageName);
        const schema = fields.map(field => `[ '${camel(pascal(field.name))}', '${field.type}' ]`).join(', ');
        const use = toClient
            ? `
/** Called with each \`${messageName}\` the server sends. */
export const activate = (api: ClientModuleApi) => {
    api.messages.on(${NAME}, (data) => {
        void data;
    });
};
`
            : `
/** Sends \`${messageName}\` to the server half (its handler: server/Messages/${type}.cs). */
export const send${pascal(messageName)} = (api: ClientModuleApi, data: ${pascal(messageName)}) => api.messages.send(${NAME}, data);
`;

        write(path.join(made, `${messageName}.ts`), `
/** \`${messageName}\`, read and sent by name: the server sends its header and fields (server/Messages/${type}.cs). */
import type { ClientModuleApi, ClientModuleMessageOf } from '@nitrodevco/client-module-sdk';

export const ${NAME} = '${messageName}';

/** The record's fields, as the server sends its schema (camelCase). */
export const ${NAME}_SCHEMA = [ ${schema} ] as const;

export type ${pascal(messageName)} = ClientModuleMessageOf<typeof ${NAME}_SCHEMA>;
${use}`);
        listMade();

        return [ 'plugin-messages.md' ];
    },

    logic() {
        if (!/^[a-z0-9_]+$/.test(name)) fail('A logic key is lower case letters, digits and _: npm run make logic skill_stove');

        const type = `${pascal(name)}Logic`;

        write(path.join(server, 'Logic', `${type}.cs`), `
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Furni;
using Turbo.Contracts.Rooms;

namespace ${assembly};

/// <summary>
/// The logic of the floor furniture whose logic is <c>${name}</c> in the hotel's furniture
/// definitions; everything it does not override is the furni's as the hotel defines it. Found as
/// the plugin loads; the furniture goes back to its old logic when it unloads.
/// </summary>
[FurniLogic("${name}")]
public sealed class ${type} : FurniLogic
{
    public override Task<bool> OnUseAsync(IFurniContext ctx, RoomUser? user, int param, CancellationToken ct) =>
        Task.FromResult(false);
}
`);

        return [ 'plugin-wired.md (Furni logic)' ];
    },

    wired() {
        const kinds = {
            effect: { prefix: 'wf_act_', base: 'WiredEffect', editor: 'WiredActionType Editor => WiredActionType.CHAT' },
            trigger: { prefix: 'wf_trg_', base: null, editor: 'WiredTriggerType Editor => WiredTriggerType.GAME_STARTS' },
            condition: { prefix: 'wf_cnd_', base: 'WiredCondition', editor: 'WiredConditionType Editor => WiredConditionType.TRIGGERER_IS_ON_FURNI' },
            selector: { prefix: 'wf_slc_', base: 'WiredSelector', editor: 'WiredSelectorType Editor => WiredSelectorType.SELECTED_FURNIS' },
        };
        const wired = kinds[name];
        const box = rest.find(arg => !arg.startsWith('--'));

        if (!wired) fail(`A wired box is one of ${Object.keys(kinds).join(', ')}: npm run make wired effect whisper_room`);
        if (!box || !/^[a-z0-9_]+$/.test(box)) fail(`A wired box is named in lower case letters, digits and _: npm run make wired ${name} whisper_room`);

        const logicKey = box.startsWith('wf_') ? box : `${wired.prefix}${box}`;
        const bare = pascal(box.replace(/^wf_[a-z]+_/, ''));
        const type = `${bare}${pascal(name)}`;
        const body = {
            effect: `
    // Show Message's editor sends three ints (who sees it, the bubble, its width): keep them.
    public override IReadOnlyList<WiredParam> Params =>
        [WiredParam.Range(0, 1, 0), WiredParam.Range(0, 1000, 0), WiredParam.Range(-1, 2, -1)];

    public override async Task<bool> ExecuteAsync(IWiredEffectContext ctx, CancellationToken ct)
    {
        var text = await ctx.FormatTextAsync(ctx.Text, ct);

        foreach (var user in ctx.RoomUsers)
            await ctx.ShowMessageAsync(user, text, onlyForThem: true, ct);

        return ctx.RoomUsers.Count > 0;
    }`,
            trigger: `
    public override bool Matches(${bare} evt, IWiredBoxConfig box) => true;`,
            condition: `
    public override bool ActsOnUsers => true;

    public override bool ActsOnFurni => true;

    // The positive rule only: the room applies the editor's "not" and invert switches.
    public override bool Evaluate(IWiredRunContext ctx) =>
        ctx.Users.Any(user => ctx.Furni.Any(furni => furni.X == user.X && furni.Y == user.Y));`,
            selector: `
    public override WiredSelection Select(IWiredRunContext ctx) =>
        WiredSelection.OfFurni(ctx.RoomFurni.Where(furni => furni.State > 0).Select(furni => furni.Id));`,
        }[name];
        const header = name === 'trigger'
            ? `/// <summary>${bare}: what the plugin fires with hotel.Wired.FireAsync (plain values: it travels as JSON).</summary>
public sealed record ${bare} : IWiredEvent;

/// <summary>
/// Starts its stack when the plugin fires a ${bare} in the room. Found as the plugin loads; the
/// furniture whose logic is <c>${logicKey}</c> is this box.
/// </summary>
[WiredBox("${logicKey}")]
public sealed class ${type} : WiredTrigger<${bare}>`
            : `/// <summary>
/// A wired ${name} of the plugin's own: the furniture whose logic is <c>${logicKey}</c> is this
/// box. Found as the plugin loads; it runs under the room's wired rules like core's boxes.
/// </summary>
[WiredBox("${logicKey}")]
public sealed class ${type} : ${wired.base}`;

        write(path.join(server, 'Wired', `${type}.cs`), `
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace ${assembly};

${header}
{
    // The official editor the client opens: pick one whose inputs are the box's.
    public override ${wired.editor};
${body}
}
`);

        return [ 'plugin-wired.md' ];
    },

    schedule() {
        const type = pascal(name);
        const every = option('every', null);
        const cron = option('cron', null);

        if (every === true || cron === true || (every && cron)) fail('A schedule is --cron "<five fields>" or --every <hh:mm:ss>: npm run make schedule weekly-rotation --cron "0 18 * * MON"');

        const perServer = option('per-server', false) === true;
        const times = every ? `Every("${every}"` : `Schedule("${cron ?? '0 0 * * MON'}"`;
        const attribute = `[${times}${perServer ? ', PerServer = true' : ''})]`;
        const runs = perServer
            ? 'on every server, each on its own clock (for what a server holds in memory),'
            : 'once per cluster';

        write(path.join(server, 'Tasks', `${type}.cs`), `
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Scheduling;

namespace ${assembly};

/// <summary>
/// Runs on its schedule, ${runs} and never twice at once; listed on the admin panel's
/// Plugins page (Schedule) with Run now. A run is cancelled through ct when the plugin unloads.
/// Found as the plugin loads.
/// </summary>
${attribute}
public sealed class ${type} : IScheduledTask
{
    public Task RunAsync(ScheduledTaskRun run, CancellationToken ct) => Task.CompletedTask;
}
`);

        return [ 'plugin-scheduler.md' ];
    },

    policy() {
        const kinds = {
            purchase: {
                contract: 'IPurchasePolicy',
                decision: 'PurchaseDecision',
                parameters: 'PurchaseRequest request, PurchaseDecision proposed, CancellationToken ct',
                body: 'ValueTask.FromResult(proposed)',
                summary: 'Asked before a catalogue purchase is paid for: return proposed to let it stand, proposed with { CostCredits = ... } to change\n/// what it costs or gives, or PurchaseDecision.Refuse("why") to refuse it.',
            },
            'room-entry': {
                contract: 'IRoomEntryPolicy',
                decision: 'RoomEntryDecision',
                parameters: 'RoomEntryRequest request, CancellationToken ct',
                body: 'ValueTask.FromResult(RoomEntryDecision.Allow)',
                summary: 'Asked before a player enters a room the hotel lets them into: RoomEntryDecision.Allow,\n/// .Refuse("why") or .Redirect(roomId).',
            },
            placement: {
                contract: 'IPlacementPolicy',
                decision: 'PlacementDecision',
                parameters: 'PlacementRequest request, CancellationToken ct',
                body: 'ValueTask.FromResult(PlacementDecision.Allow)',
                summary: 'Asked before a player places a furni or saves a floor plan: PlacementDecision.Allow or\n/// .Refuse("why").',
            },
            registration: {
                contract: 'IRegistrationPolicy',
                decision: 'RegistrationDecision',
                parameters: 'RegistrationRequest request, CancellationToken ct',
                body: 'ValueTask.FromResult(RegistrationDecision.Allow)',
                summary: 'Asked before a new account is made (its name, look, source and address): RegistrationDecision.Allow\n/// or .Refuse("why").',
            },
        };
        const policy = kinds[name];

        if (!policy) fail(`A policy is one of ${Object.keys(kinds).join(', ')}: npm run make policy purchase club-discount`);

        const type = `${pascal(rest.find(arg => !arg.startsWith('--')) ?? name)}Policy`;

        write(path.join(server, 'Policies', `${type}.cs`), `
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Policies;

namespace ${assembly};

/// <summary>
/// ${policy.summary}
/// Found as the plugin loads; several run in Order, then plugin key.
/// </summary>
public sealed class ${type} : ${policy.contract}
{
    public ValueTask<${policy.decision}> DecideAsync(${policy.parameters}) =>
        ${policy.body};
}
`);

        return [ 'plugin-policies.md' ];
    },

    listener() {
        const type = `${pascal(name)}Listener`;

        write(path.join(server, 'Listeners', `${type}.cs`), `
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;

namespace ${assembly};

/// <summary>
/// Hears every room's events. It runs in the room's turn: keep it quick, and hand slow work
/// (database, HTTP) to a task. Found as the plugin loads.
/// </summary>
public sealed class ${type} : IRoomEventListener
{
    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct) => Task.CompletedTask;
}
`);

        return [ 'plugin-room-events.md' ];
    },

    setting() {
        const type = option('type', 'int');
        const csType = FIELD_TYPES[type];

        if (!csType) fail(`${type}: a setting is one of ${Object.keys(FIELD_TYPES).join(', ')}`);

        const fallback = option('default', null);
        const value = fallback === null || fallback === true
            ? { int: '0', long: '0', bool: 'false', string: 'string.Empty', double: '0' }[type]
            : type === 'string' ? JSON.stringify(fallback) : fallback;
        const property = `
    /// <summary>${pascal(name)} (the admin panel's Settings page shows this line).</summary>
    public ${csType} ${pascal(name)} { get; init; } = ${value};
`;
        const settings = readdirSync(server).find(file => file.endsWith('.cs') && readFileSync(path.join(server, file), 'utf8').includes('[PluginSettings('));

        if (!settings) {
            write(path.join(server, `${assembly}Settings.cs`), `
using Turbo.Contracts.Plugins;

namespace ${assembly};

/// <summary>The plugin's settings, under <c>${assembly}</c> in the hotel's settings.</summary>
[PluginSettings("${assembly}")]
public sealed class ${assembly}Settings
{${property}}
`);
        } else {
            change(path.join(server, settings), (source) => {
                if (new RegExp(`\\b${pascal(name)}\\s*\\{`).test(source)) fail(`${settings} has ${pascal(name)} already.`);

                const end = source.lastIndexOf('}');

                return `${source.slice(0, end).trimEnd()}\n${property}}\n`;
            });
        }

        return [ 'settings.md' ];
    },

    route() {
        const method = String(option('method', 'GET')).toUpperCase();
        const anonymous = option('anonymous', false) === true;
        const type = `${pascal(name.replace(/\{[^}]*\}/g, ''))}Route`;

        write(path.join(server, 'Routes', `${type}.cs`), `
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Web;

namespace ${assembly};

/// <summary>
/// <c>${method} /api/plugins/${key}/${name}</c>${anonymous ? ', for anyone' : ', for signed-in players'}; the client
/// calls it with <c>api.http</c>. Found as the plugin loads.
/// </summary>
public sealed class ${type} : IPluginWebRoute
{
    public string Method => "${method}";

    public string Pattern => "${name}";
${anonymous ? '\n    public bool Anonymous => true;\n' : ''}
    public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct) =>
        Task.FromResult(PluginWebResult.Ok(new { }));
}
`);

        return [ 'plugin-web-routes.md' ];
    },

    window() {
        if (!/^[a-z0-9_-]+$/.test(name)) fail('A window is lower case letters, digits, _ and -: npm run make window news');

        const type = `${pascal(name)}Window`;

        text(`${key}.${name}.title`, pascal(name));
        write(path.join(made, `${type}.tsx`), `
import { type ClientModuleApi, type ClientModuleWindowProps, useApi } from '@nitrodevco/client-module-sdk';
import { Box, Frame, ThemeText } from '@nitrodevco/client-module-sdk/theme';

/** The ${name} window: opened by the link \`${key}/${name}\` (\`api.windows.open('${name}')\` in code). */
export const ${type} = ({ close }: ClientModuleWindowProps) => {
    const api = useApi();
    const title = api.texts.get('${key}.${name}.title', '${pascal(name)}');

    return (
        <Frame id={\`\${api.key}-${name}\`} variant="3" caption={title} centered resizeDirection="none" onClose={close} layout={{ width: 240, height: 140 }}>
            <Box layout={{ flexDirection: 'column', gap: 8, padding: 10, width: 238 }}>
                <ThemeText text={title} textStyle="u_regular" />
            </Box>
        </Frame>
    );
};

export const activate = (api: ClientModuleApi) => {
    api.windows.register('${name}', ${type});
    api.links.handle('${key}', (link) => {
        if (link === '${key}/${name}') api.windows.open('${name}');
    });
};
`);
        listMade();

        return [ 'nitro-next docs/client-modules.md', 'nitro-next docs/template-patches.md (a core window)' ];
    },

    patch() {
        if (!/^[a-z0-9_-]+\/[a-z0-9_]+$/i.test(name)) fail('A patch names a window\'s template, <library>/<asset>: npm run make patch habbo-toolbar-com/purse_xml');

        change(path.join(root, 'wwwroot', 'template-patches.json'), (json) => {
            const patches = json ? JSON.parse(json) : { patches: [] };

            if (patches.patches.some(patch => patch.template === name)) fail(`wwwroot/template-patches.json patches ${name} already.`);

            patches.patches.push({ template: name, ops: [] });

            return `${JSON.stringify(patches, null, 4)}\n`;
        });

        return [ 'nitro-next docs/template-patches.md (ops set, remove and insert elements by their layout names)' ];
    },

    migration() {
        if (!/^[A-Za-z][A-Za-z0-9]*$/.test(name)) fail('A migration is a C# name: npm run make migration AddPrizes');

        const context = `${assembly}Db`;
        const contextFile = path.join(server, 'Database', `${context}.cs`);

        if (!manifest.TablePrefix) {
            manifest.TablePrefix = `${key}_`;
            change(manifestFile, () => `${JSON.stringify(manifest, null, 4)}\n`);
        }

        for (const dll of [ 'Turbo.Database.dll', 'Microsoft.EntityFrameworkCore.dll', 'Microsoft.EntityFrameworkCore.Abstractions.dll', 'Microsoft.EntityFrameworkCore.Relational.dll', 'Pomelo.EntityFrameworkCore.MySql.dll' ]) {
            reference(dll, 'The plugin\'s tables (turbo-cloud docs/database.md).');
        }

        // dotnet ef needs EF Core's design package, at the server's EF Core version, while it runs only.
        change(path.join(server, `${assembly}.csproj`), (project) => {
            if (project.includes('Microsoft.EntityFrameworkCore.Design')) return project;

            const end = project.lastIndexOf('</Project>');

            return `${project.slice(0, end)}  <ItemGroup Condition="'$(DesignTime)' == 'true'">
    <!-- dotnet ef (npm run make migration), at the server's EF Core version. -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="${efVersion()}" PrivateAssets="all" />
  </ItemGroup>
${project.slice(end)}`;
        });

        if (!existsSync(contextFile)) {
            write(contextFile, `
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;

namespace ${assembly};

/// <summary>
/// The plugin's tables, named with its manifest's TablePrefix. Found as the plugin loads, which
/// runs its migrations (server/Database/Migrations, made by <c>npm run make migration Name</c>).
/// A route, task or listener is built once: it takes IServiceScopeFactory and reads the tables in
/// a scope of its own (turbo-cloud docs/database.md).
/// </summary>
public sealed class ${context} : PluginDbContextBase<${context}>
{
    public DbSet<${assembly}Row> Rows => Set<${assembly}Row>();
}

[Table("rows")]
public sealed class ${assembly}Row
{
    public int Id { get; set; }
}
`);
        }

        const args = [ 'ef', 'migrations', 'add', name, '--project', path.join(server, `${assembly}.csproj`), '--context', context, '--output-dir', path.join('Database', 'Migrations') ];

        if (process.env.TURBO_MAKE_EF === '0') {
            console.log(`Then: DesignTime=true dotnet ${args.join(' ')}`);
        } else {
            const env = { ...process.env, DesignTime: 'true' };
            const dotnet = (command, stdio = 'inherit') => spawnSync('dotnet', command, { cwd: root, stdio, env }).status === 0;
            // Built here, so a plugin that doesn't build says why instead of dotnet ef's "Build failed".
            const errors = checkHost() ?? buildErrors(path.join(server, `${assembly}.csproj`), env);

            if (errors) fail(`${errors}\nThe plugin does not build, so no migration was made.`);

            if (!dotnet([ 'ef', '--version' ], 'ignore')) fail('dotnet ef is not installed: dotnet tool install --global dotnet-ef (once per machine), then run this again.');

            if (!dotnet([ ...args, '--no-build' ])) fail(`dotnet ef failed (above). Run it again with: DesignTime=true dotnet ${args.join(' ')}`);
        }

        return [ 'database.md' ];
    },
};

if (!recipes[kind] || !name) {
    fail(`npm run make <kind> <name>, a kind of: ${Object.keys(recipes).join(', ')} (tools/make.mjs says more)`);
}

const guide = recipes[kind]();

for (const file of written) console.log(`  ${file}`);
console.log(`Read: ${guide.map(doc => (doc.startsWith('nitro-next') ? doc : DOCS + doc)).join(', ')}`);
