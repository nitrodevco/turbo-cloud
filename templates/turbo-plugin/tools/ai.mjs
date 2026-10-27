/**
 * `npm run ai` / `npm run ai:update`: sets the plugin up for AI assistants (Turbo Boost, turbo-cloud
 * docs/turbo-boost.md), from the server the plugin builds against (TurboBin's plugin-sdk/ai):
 *
 *   - CLAUDE.md and AGENTS.md: the kit's guideline, the plugin's own (.ai/guidelines/*.md), those of
 *     the plugins it depends on (their ai/guidelines.md) and the index of its rules (.ai/rules), in a
 *     <turbo-boost> block; everything outside the block is left as it is;
 *   - the skills (the kit's, the dependencies' ai/skills, the plugin's .ai/skills) in .claude/skills
 *     and .agents/skills;
 *   - .mcp.json: the dev hotel's MCP server (turbo-mcp-plugin), its token from TURBO_MCP_TOKEN.
 *
 *   node tools/ai.mjs [install|update] [--agents claude,codex] [--mcp-url <url>] [--plugins <dir>,<dir>]
 *   node tools/ai.mjs rule "<title>" "<the rule>"     a project rule in .ai/rules, then update
 *
 * What it chose is kept in .ai/turbo-boost.json, so `update` writes the same again. `dotnet new`
 * copies this file as it is (template.json's copyOnly): its folder names are not the sample's.
 */
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { turboBin } from './host.mjs';

const root = path.resolve(import.meta.dirname, '..');
const manifest = JSON.parse(readFileSync(path.join(root, 'manifest.json'), 'utf8'));
const key = manifest.Key;
const own = path.join(root, '.ai');
const stateFile = path.join(own, 'turbo-boost.json');
const BEGIN = '<turbo-boost>';
const END = '</turbo-boost>';
const AGENTS = {
    claude: { guidelines: 'CLAUDE.md', skills: path.join('.claude', 'skills'), mcp: true },
    codex: { guidelines: 'AGENTS.md', skills: path.join('.agents', 'skills'), mcp: false },
};
const DEFAULT_MCP_URL = 'http://127.0.0.1:8092/api/plugins/mcp/mcp';

const [ command = 'install', ...rest ] = process.argv.slice(2);

const fail = (text) => {
    console.error(text);
    process.exit(1);
};

const option = (flag) => {
    const at = rest.indexOf(`--${flag}`);

    return (at >= 0 && rest[at + 1] && !rest[at + 1].startsWith('--')) ? rest[at + 1] : null;
};

const read = file => (existsSync(file) ? readFileSync(file, 'utf8') : null);

const readJson = (file, fallback) => {
    const text = read(file);

    if (text === null) return fallback;

    try {
        return JSON.parse(text);
    } catch {
        return fail(`${path.relative(root, file)} is not JSON: fix or delete it, then run this again.`);
    }
};

const folders = parent => (existsSync(parent) ? readdirSync(parent).filter(name => statSync(path.join(parent, name)).isDirectory()).sort() : []);

/** `name` and `description` of a SKILL.md's front matter. */
const frontMatter = (text) => {
    const block = /^---\r?\n([\s\S]*?)\r?\n---/.exec(text)?.[1] ?? '';
    const field = name => new RegExp(`^${name}:\\s*(.+)$`, 'm').exec(block)?.[1].trim() ?? null;

    return { name: field('name'), description: field('description') };
};

/**
 * The kit: TurboBin's plugin-sdk/ai, the one of the server the plugin builds against, else the
 * --turbo checkout's ai/ (a server built before the kit has none).
 */
const findKit = () => {
    const bin = turboBin();
    const shipped = path.join(bin, 'plugin-sdk', 'ai');

    if (existsSync(shipped)) return { folder: shipped, docs: path.join(bin, 'plugin-sdk', 'docs'), from: `TurboBin (${bin})` };

    const checkout = process.env.TurboCloudPath ?? path.resolve(root, '../turbo-cloud');

    if (existsSync(path.join(checkout, 'ai'))) return { folder: path.join(checkout, 'ai'), docs: path.join(checkout, 'docs'), from: `the turbo-cloud checkout (${checkout})` };

    return fail(`No AI kit: ${shipped} is not there (a server built before Turbo Boost?), nor ${path.join(checkout, 'ai')}. Build the server again from a current turbo-cloud, or set TurboBin to the bin folder of the server you run.`);
};

/** A dependency's key, whether the manifest lists keys or { Key } objects. */
const dependencyKeys = plugin => (plugin.Dependencies ?? []).map(dependency => (typeof dependency === 'string' ? dependency : dependency.Key)).filter(Boolean);

/**
 * The folders of the plugins this one depends on, directly or through another, dependencies first:
 * beside this plugin (../<key>, or any sibling whose manifest has the key) or in the --plugins /
 * TurboPlugins folders.
 */
const findDependencies = (searchPaths) => {
    const candidates = [ path.dirname(root), ...searchPaths ].flatMap(parent => folders(parent).map(name => path.join(parent, name)));
    const byKey = new Map();

    for (const folder of candidates) {
        const found = readJson(path.join(folder, 'manifest.json'), null);

        if (found?.Key && !byKey.has(found.Key) && path.resolve(folder) !== root) byKey.set(found.Key, { folder, manifest: found });
    }

    const ordered = [];
    const missing = [];
    const visit = (dependencyKey, seen) => {
        if (seen.has(dependencyKey) || ordered.some(x => x.manifest.Key === dependencyKey)) return;

        const found = byKey.get(dependencyKey);

        if (!found) {
            if (!missing.includes(dependencyKey)) missing.push(dependencyKey);

            return;
        }

        dependencyKeys(found.manifest).forEach(next => visit(next, new Set([ ...seen, dependencyKey ])));
        ordered.push(found);
    };

    dependencyKeys(manifest).forEach(dependencyKey => visit(dependencyKey, new Set([ key ])));

    return { dependencies: ordered, missing };
};

const slug = title => title.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 60);

/** Each rule's first line (its title), for the index. */
const rules = () => {
    const folder = path.join(own, 'rules');

    if (!existsSync(folder)) return [];

    return readdirSync(folder).filter(file => file.endsWith('.md')).sort().map((file) => {
        const first = readFileSync(path.join(folder, file), 'utf8').split(/\r?\n/).find(line => line.trim()) ?? file;

        return { file: `.ai/rules/${file}`, title: first.replace(/^#+\s*/, '').trim() };
    });
};

const markdownFiles = folder => (existsSync(folder) ? readdirSync(folder).filter(file => file.endsWith('.md')).sort().map(file => path.join(folder, file)) : []);

/** The block's text: the guideline, this plugin, its own guidelines, its dependencies', its rules and the skills. */
const block = (kit, dependencies, skills) => {
    const parts = [
        `<!-- Written by \`npm run ai\` from ${kit.from.replace(/\\/g, '/')}. Edit outside this block: \`npm run ai:update\` replaces it. -->`,
        '',
        ...markdownFiles(path.join(kit.folder, 'guidelines')).map(file => readFileSync(file, 'utf8').trim()),
        '',
        '### This plugin',
        '',
        `- Name \`${manifest.Name}\`, key \`${key}\`: messages \`${key}.*\`, permission nodes \`${key}.*\`, texts \`${key}.*\`, web routes \`/api/plugins/${key}/...\`${manifest.TablePrefix ? `, tables \`${manifest.TablePrefix}*\`` : ''}.`,
        `- The server it builds against: \`${turboBin().replace(/\\/g, '/')}\`; its guides: \`${kit.docs.replace(/\\/g, '/')}\`.`,
    ];

    for (const file of markdownFiles(path.join(own, 'guidelines'))) parts.push('', readFileSync(file, 'utf8').trim());

    if (dependencies.length) {
        parts.push('', '### Plugins this one depends on');

        for (const { folder, manifest: dependency } of dependencies) {
            const guideline = read(path.join(folder, 'ai', 'guidelines.md'));

            parts.push('', `#### ${dependency.Name ?? dependency.Key} (\`${dependency.Key}\`)`, '', guideline ? guideline.trim() : `No guideline: read its README (${path.relative(root, folder).replace(/\\/g, '/')}).`);
        }
    }

    const ruleList = rules();

    if (ruleList.length) {
        parts.push('', '### Project rules', '', 'Rules of this plugin; read the one that applies before changing that code.', '');
        ruleList.forEach(rule => parts.push(`- [${rule.title}](${rule.file})`));
    }

    if (skills.length) {
        parts.push('', '### Skills', '', 'Read the skill whose description matches the task before writing that kind of code (`.claude/skills/<name>/SKILL.md`, `.agents/skills/<name>/SKILL.md`):', '');
        skills.forEach(skill => parts.push(`- \`${skill.name}\`: ${skill.description}`));
    }

    return `${BEGIN}\n${parts.join('\n')}\n${END}`;
};

/** `file` with its block replaced (or added at the end); what is outside it kept byte for byte. */
const writeBlock = (file, text) => {
    const before = read(file) ?? '';
    const start = before.indexOf(BEGIN);
    const end = before.indexOf(END, start);
    let after;

    if (start >= 0 && end > start) after = before.slice(0, start) + text + before.slice(end + END.length);
    else if (start >= 0) fail(`${path.relative(root, file)} has ${BEGIN} and no ${END}: fix it by hand, then run this again.`);
    else after = before ? `${before.replace(/\s*$/, '')}\n\n${text}\n` : `${text}\n`;

    if (after !== before) writeFileSync(file, after);

    return after !== before;
};

/** Every skill to install, by name: the kit's, then the dependencies', then the plugin's own (each replacing one of its name). */
const collectSkills = (kit, dependencies) => {
    const byName = new Map();
    const add = (folder, from) => {
        for (const name of folders(folder)) {
            const text = read(path.join(folder, name, 'SKILL.md'));

            if (!text) continue;

            const { description } = frontMatter(text);

            if (frontMatter(text).name !== name) console.warn(`${path.join(folder, name)}: its SKILL.md's name is not "${name}"; installed as "${name}".`);

            byName.set(name, { name, description: description ?? '', folder: path.join(folder, name), from });
        }
    };

    add(path.join(kit.folder, 'skills'), 'kit');
    dependencies.forEach(({ folder, manifest: dependency }) => add(path.join(folder, 'ai', 'skills'), dependency.Key));
    add(path.join(own, 'skills'), 'own');

    return [ ...byName.values() ].sort((a, b) => a.name.localeCompare(b.name));
};

const installSkills = (target, skills, before) => {
    const folder = path.join(root, target);

    mkdirSync(folder, { recursive: true });

    // Only what the last run installed is taken away: a skill the author put there stays.
    for (const name of before) {
        if (!skills.some(skill => skill.name === name)) rmSync(path.join(folder, name), { recursive: true, force: true });
    }

    for (const skill of skills) {
        rmSync(path.join(folder, skill.name), { recursive: true, force: true });
        cpSync(skill.folder, path.join(folder, skill.name), { recursive: true });
    }
};

const writeMcp = (url) => {
    const file = path.join(root, '.mcp.json');
    const config = readJson(file, {});

    config.mcpServers = {
        ...config.mcpServers,
        turbo: { type: 'http', url, headers: { Authorization: 'Bearer ${TURBO_MCP_TOKEN}' } },
    };
    writeFileSync(file, `${JSON.stringify(config, null, 4)}\n`);
};

const install = () => {
    const state = readJson(stateFile, {});
    const agents = (option('agents') ?? state.agents?.join(',') ?? Object.keys(AGENTS).join(',')).split(',').map(x => x.trim()).filter(Boolean);
    const unknown = agents.filter(agent => !AGENTS[agent]);

    if (unknown.length) fail(`${unknown.join(', ')}: an agent is one of ${Object.keys(AGENTS).join(', ')}.`);

    const url = option('mcp-url') ?? state.mcpUrl ?? DEFAULT_MCP_URL;
    const searchPaths = (option('plugins') ?? process.env.TurboPlugins ?? state.plugins?.join(',') ?? '').split(/[,;]/).map(x => x.trim()).filter(Boolean).map(x => path.resolve(root, x));
    const kit = findKit();
    const { dependencies, missing } = findDependencies(searchPaths);
    const skills = collectSkills(kit, dependencies);
    const text = block(kit, dependencies, skills);
    const written = [];

    for (const agent of agents) {
        const { guidelines, skills: target, mcp } = AGENTS[agent];

        if (writeBlock(path.join(root, guidelines), text)) written.push(guidelines);

        installSkills(target, skills, state.skills ?? []);
        written.push(`${target.replace(/\\/g, '/')}/ (${skills.length} skills)`);

        if (mcp) {
            writeMcp(url);
            written.push('.mcp.json');
        }
    }

    mkdirSync(own, { recursive: true });
    writeFileSync(stateFile, `${JSON.stringify({ agents, mcpUrl: url, plugins: searchPaths.map(x => path.relative(root, x)), skills: skills.map(skill => skill.name) }, null, 4)}\n`);

    console.log(`Turbo Boost from ${kit.from}:`);
    written.forEach(file => console.log(`  ${file}`));

    if (dependencies.length) console.log(`  with ${dependencies.map(x => x.manifest.Key).join(', ')}`);
    if (missing.length) console.warn(`  not found, skipped: ${missing.join(', ')} (put them beside this plugin, or name their folder with --plugins <dir>)`);

    console.log(`
The dev tools (search_docs, plugin_errors, server_log, message_schema, plugin_tables) are the dev
hotel's MCP server at ${url}:
  1. load the MCP plugin (turbo-mcp-plugin) on your dev hotel, with Turbo:Web:Enabled;
  2. admin panel > MCP > New token (read-only), and give your rank plugin.mcp.use and the
     plugin.mcp.tool.* nodes of the tools;
  3. set TURBO_MCP_TOKEN=tmcp_... where the assistant runs (Claude Code reads .mcp.json; other
     agents: add this address as an HTTP MCP server with that bearer token).`);
};

const recordRule = () => {
    const [ title, ...words ] = rest.filter(arg => !arg.startsWith('--'));

    if (!title || !words.length) fail('npm run ai:rule -- "<title>" "<the rule: what to do, and why>"');

    const folder = path.join(own, 'rules');
    const file = path.join(folder, `${slug(title)}.md`);

    mkdirSync(folder, { recursive: true });
    writeFileSync(file, `# ${title}\n\n${words.join(' ')}\n`);
    console.log(`${path.relative(root, file)}`);
    install();
};

if (command === 'install' || command === 'update') install();
else if (command === 'rule') recordRule();
else fail(`${command}: one of install, update, rule.`);
