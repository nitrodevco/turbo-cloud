#!/usr/bin/env node
// Compares the client module SDK the server bundles plugins' client modules with
// (Turbo.Plugins/ClientBuild/sdk/) with nitro-next's packages/client-module-sdk, and with --write
// takes nitro-next's files and records their pins (Turbo.Plugins/ClientBuild/sdk-pins.json, which
// Turbo.Tests checks).
//
//   node scripts/client-module-sdk.mjs [nitro-next folder] [--ref <git ref>] [--write]
//
// The folder defaults to $NITRO_NEXT, then ../nitro-next beside this repository. Without --ref the
// checkout's files are read; with it, the files at that commit (--ref origin/pixi-render).
// Exits 1 when the copy, nitro-next and the pins do not all agree.
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const REPO = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const SDK = join(REPO, 'Turbo.Plugins', 'ClientBuild', 'sdk');
const PINS = join(REPO, 'Turbo.Plugins', 'ClientBuild', 'sdk-pins.json');
const PINS_NAME = 'Turbo.Plugins/ClientBuild/sdk-pins.json';
const SOURCE = 'packages/client-module-sdk/src';
// The runtime files; the types (*.d.ts) are only taken out by the build, so the server needs none.
const RUNTIME = [ 'host.ts', 'index.ts', 'theme.ts' ];

const args = process.argv.slice(2);
const write = args.includes('--write');
const refAt = args.indexOf('--ref');
const ref = refAt >= 0 ? args[refAt + 1] : undefined;
const folder = resolve(args.find((x, i) => !x.startsWith('--') && (refAt < 0 || i !== refAt + 1)) ?? process.env.NITRO_NEXT ?? join(REPO, '..', 'nitro-next'));

if (!existsSync(join(folder, SOURCE))) {
    console.error(`No nitro-next checkout at ${folder} (${SOURCE} is missing): pass its folder or set NITRO_NEXT.`);
    process.exit(2);
}

const git = (...gitArgs) => execFileSync('git', [ '-C', folder, ...gitArgs ], { encoding: 'utf8' });
const lf = text => text.replace(/\r\n/g, '\n');
const read = file => lf(ref ? git('show', `${ref}:${SOURCE}/${file}`) : readFileSync(join(folder, SOURCE, file), 'utf8'));
const sha256 = text => createHash('sha256').update(lf(text), 'utf8').digest('hex');

/** What each copied file should hold, by its path under sdk/. */
const expected = {};

for (const file of RUNTIME)
    expected[`client-module-sdk/${file}`] = `// nitro-next ${SOURCE}/${file}, as the server bundles plugins' client modules with it.\n${read(file)}`;

// The shared libraries are what the bundler preset (build.js) answers for each import, run here.
const scratch = mkdtempSync(join(tmpdir(), 'client-module-sdk-'));

try {
    const preset = join(scratch, 'build.mjs');
    writeFileSync(preset, read('build.js'));

    const { HOST_LIBRARIES, clientModuleHostPlugin } = await import(pathToFileURL(preset).href);
    let load;

    clientModuleHostPlugin().setup({ onResolve() {}, onLoad(_, handler) { load = handler; } });

    for (const [ path, name ] of Object.entries(HOST_LIBRARIES))
        expected[`shared/${path.includes('/') ? path : `${path}/index`}.js`] =
            `// The client's own ${name}, as the client module SDK's bundler preset (clientModuleHostPlugin) reads it:\n// a module never bundles a second copy.\n${load({ path }).contents}\n`;
} finally {
    rmSync(scratch, { recursive: true, force: true });
}

const copied = {};
const walk = dir => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
        const path = join(dir, entry.name);

        if (entry.isDirectory()) walk(path);
        else if (/\.(ts|js)$/.test(entry.name)) copied[relative(SDK, path).replaceAll('\\', '/')] = lf(readFileSync(path, 'utf8'));
    }
};

walk(SDK);

const commit = git('rev-parse', ref ?? 'HEAD').trim();
let differs = 0;

for (const file of Object.keys(expected).sort()) {
    const state = !(file in copied) ? 'missing' : copied[file] === expected[file] ? 'same' : 'differs';

    if (state !== 'same') differs++;

    console.log(`${state.padEnd(8)} ${file}`);

    if (write && state !== 'same') writeFileSync(join(SDK, file), expected[file]);
}

for (const file of Object.keys(copied).filter(x => !(x in expected)).sort()) {
    differs++;
    console.log(`extra    ${file} (no nitro-next source: remove it, or teach this script where it comes from)`);
}

if (!ref && git('status', '--porcelain', '--', SOURCE).trim())
    console.log(`note     ${SOURCE} has uncommitted changes in ${folder}; the pins name ${commit.slice(0, 9)}, which does not hold them`);

const pins = {
    nitroNextCommit: commit,
    files: Object.fromEntries(Object.keys(expected).sort().map(file => [ file, sha256(expected[file]) ])),
};
const pinsText = `${JSON.stringify(pins, null, 2)}\n`;
const current = existsSync(PINS) ? JSON.parse(readFileSync(PINS, 'utf8')) : undefined;
const pinsAgree = current !== undefined && JSON.stringify(current.files) === JSON.stringify(pins.files);

if (write) {
    if (!pinsAgree || current.nitroNextCommit !== commit) writeFileSync(PINS, pinsText);

    console.log(`wrote the copy and ${PINS_NAME} at nitro-next ${commit.slice(0, 9)}`);
} else {
    console.log(`${pinsAgree ? 'pins     agree' : 'pins     differ'} (${PINS_NAME}, nitro-next ${current?.nitroNextCommit?.slice(0, 9) ?? 'none'})`);

    if (differs || !pinsAgree) {
        console.log(`nitro-next ${commit.slice(0, 9)} and the copy disagree: run again with --write, then commit the copy and the pins`);
        process.exit(1);
    }
}
