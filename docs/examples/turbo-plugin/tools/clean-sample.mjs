/**
 * `npm run clean-sample`: takes the template's sample (the skills of the plugin `dotnet new` made:
 * its book, command, messages, permission node, weekly reset, settings, window, toolbar icon, purse
 * button and their tests and texts) out of the plugin, leaving an empty plugin that loads, to build
 * up with `npm run make`. What `npm run make` added is left as it is. Run it once, before the
 * plugin is yours: it deletes the sample's files whatever is in them.
 */
import { existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { projectName } from './host.mjs';

const root = path.resolve(import.meta.dirname, '..');
const manifest = JSON.parse(readFileSync(path.join(root, 'manifest.json'), 'utf8'));
const key = manifest.Key;
const assembly = projectName();
const removed = [];

const remove = (...parts) => {
    const file = path.join(root, ...parts);

    if (!existsSync(file)) return;

    rmSync(file);
    removed.push(path.relative(root, file));
};

for (const file of [ 'SkillBook.cs', `${assembly}Command.cs`, `${assembly}Messages.cs`, `${assembly}Permissions.cs`, `${assembly}Settings.cs`, `${assembly}WeeklyReset.cs` ]) remove('server', file);
remove('tests', `${assembly}.Tests`, `${assembly}Tests.cs`);
for (const file of [ 'skill.ts', 'skillStore.ts', `${assembly}Window.tsx` ]) remove('client', 'src', file);
remove('client', 'tests', 'skill.test.ts');
remove('wwwroot', `${key}.png`);

const rewrite = (file, content) => {
    writeFileSync(path.join(root, file), content.replace(/^\n/, ''));
    removed.push(`${file} (rewritten)`);
};

rewrite(path.join('server', `${assembly}Plugin.cs`), `
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;

namespace ${assembly};

/// <summary>
/// The plugin's services, for its routes, tasks, commands and handlers to take by constructor. The
/// rest is found as it loads: its key and version in manifest.json, its settings, commands,
/// permission nodes, messages, handlers, routes and tasks in the assembly, and its client module,
/// built by the hotel from client/src. All of it goes when it unloads.
/// </summary>
public sealed class ${assembly}Plugin : TurboPlugin
{
    protected override void Services(IServiceCollection services) { }
}
`);

rewrite(path.join('client', 'src', 'index.tsx'), `
/**
 * The plugin's client module: the hotel builds it from client/src into one file, \`${key}.js\`,
 * which the server lists for every client that runs modules.
 */
import { defineModule } from '@nitrodevco/client-module-sdk';

import { activateMade } from './made';

export default defineModule({
    activate(api) {
        // What \`npm run make\` added (windows, message listeners).
        activateMade(api);
    },
});
`);

rewrite(path.join('wwwroot', 'template-patches.json'), `${JSON.stringify({ patches: [] }, null, 4)}\n`);

// The sample's texts; the ones \`npm run make\` added stay.
const textsFile = path.join(root, 'gamedata', 'external_texts.txt');
const sampleTexts = [ 'window.title', 'window.level', 'window.experience', 'window.train', 'toolbar.tooltip', 'purse.button', 'levelled' ].map(name => `${key}.${name}=`);

if (existsSync(textsFile)) {
    const texts = readFileSync(textsFile, 'utf8').split(/\r?\n/);

    writeFileSync(textsFile, texts.filter(line => !sampleTexts.some(text => line.startsWith(text))).join('\n'));
    removed.push(path.join('gamedata', 'external_texts.txt (the sample\'s texts)'));
}

for (const file of removed) console.log(`  ${file}`);
console.log('The sample is out: npm run make <kind> <name> adds what the plugin needs (tools/make.mjs lists the kinds).');
