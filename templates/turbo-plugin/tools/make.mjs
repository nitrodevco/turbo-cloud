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
 *   listener <name>                     a room event listener (server/Listeners)
 *   setting <name> [--type int|long|bool|string|double] [--default <value>]
 *                                       a setting in the plugin's [PluginSettings] class
 *   route <path> [--method GET] [--anonymous]
 *                                       a web route at /api/plugins/<key>/<path> (server/Routes)
 *   window <name>                       a window, opened by the link <key>/<name> (client/src/made)
 *   patch <library>/<asset>             a template patch on a core window (wwwroot/template-patches.json)
 *   migration <Name>                    the plugin's tables (server/Database) and a migration (dotnet ef)
 *
 * A file that is there already is left alone (the command says so and stops).
 */
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

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
    const bin = process.env.TurboBin ?? path.resolve(root, '../turbo-cloud', 'Turbo.Main', 'bin', 'Debug', 'net10.0');
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

        reference('Turbo.Rooms.dll', 'Furni logic base classes (FurnitureFloorLogic): not in Turbo.Primitives yet.');
        write(path.join(server, 'Logic', `${type}.cs`), `
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace ${assembly};

/// <summary>
/// The logic of the furniture whose logic is <c>${name}</c> in the hotel's furniture definitions.
/// Found as the plugin loads; the furniture goes back to its old logic when it unloads.
/// </summary>
[RoomObjectLogic("${name}")]
public sealed class ${type}(IStuffDataFactory stuffData, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffData, ctx);
`);

        return [ 'extensions.md (Furni and wired logic)' ];
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
/// runs its migrations (server/Database/Migrations, made by \`npm run make migration <Name>\`).
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
            const dotnet = command => spawnSync('dotnet', command, { cwd: root, stdio: 'inherit', env }).status === 0;

            if (!dotnet([ 'restore', path.join(server, `${assembly}.csproj`) ]) || !dotnet(args)) fail(`dotnet ef failed (once per machine: dotnet tool install --global dotnet-ef). Run again with: DesignTime=true dotnet ${args.join(' ')}`);
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
