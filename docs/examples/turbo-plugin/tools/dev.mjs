/**
 * The plugin's one build script, run through npm (`npm run dev`, `build`, `check`, `package`, `test`):
 *
 *   dev      copies both halves' sources into dist/<key>/ and keeps it current: a changed .cs is
 *            copied (the hotel compiles the plugin's sources and reloads it; a compile error is in
 *            its log and the admin panel as server/File.cs(line,column)), a changed client file is
 *            copied (the hotel builds the module again and sends clients its new version, which
 *            they run without a page reload).
 *   build    the same once, then checks the server sources compile, and stops.
 *   check    compiles the server sources as the hotel does (the project imports the server's
 *            plugin-sdk/Turbo.Plugin.props: the same references, C# and policy), and stops.
 *   package  dist/<key>/ afresh and dist/<key>-<version>.zip, the package the admin panel's plugin
 *            upload takes: the sources, never an assembly.
 *   test     the server tests (dotnet test) and the client tests (node --test).
 *
 * dist/<key>/ is the plugin folder a hotel loads: name it once in the hotel's
 * Turbo:Plugin:DevPluginPaths and run the server in Development. The client module is built by the
 * hotel, from client/src/ as it is (turbo-cloud docs/client-modules.md): a package holds no built
 * JavaScript, and the module's build errors are in the server log and the panel's plugin list.
 */
import { spawn } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, watch, writeFileSync } from 'node:fs';
import path from 'node:path';
import { crc32, deflateRawSync } from 'node:zlib';

import { buildHint, checkHost, projectName } from './host.mjs';

const root = path.resolve(import.meta.dirname, '..');
const manifest = JSON.parse(readFileSync(path.join(root, 'manifest.json'), 'utf8'));
const key = manifest.Key;
const assembly = projectName();
const dist = path.join(root, 'dist', key);
const serverProject = path.join(root, 'server', `${assembly}.csproj`);
const mode = process.argv[2] ?? 'dev';
const release = mode === 'package';

const log = (part, text) => console.log(`${new Date().toISOString().slice(11, 23)} [${part}] ${text}`);
const since = start => `${Math.round(performance.now() - start)} ms`;

const run = (command, args, options = {}) => new Promise((resolve) => {
    const child = spawn(command, args, { cwd: root, stdio: options.quiet ? [ 'ignore', 'pipe', 'pipe' ] : 'inherit', shell: process.platform === 'win32' });
    let output = '';

    child.stdout?.on('data', (data) => {
        output += data;
    });
    child.stderr?.on('data', (data) => {
        output += data;
    });
    child.on('close', code => resolve({ ok: code === 0, output }));
});

/** The client's packages for its types and tests, installed the first time (the SDK comes from the path in package.json). */
const ensurePackages = async () => {
    if (existsSync(path.join(root, 'node_modules', '@nitrodevco', 'client-module-sdk'))) return;

    log('setup', 'npm install');

    if (!(await run('npm', [ 'install', '--no-audit', '--no-fund' ])).ok) throw new Error('npm install failed');
};

/** The plugin's own files beside the assembly: its wwwroot/ and gamedata/. manifest.json is written last. */
const copyFiles = () => {
    for (const folder of [ 'wwwroot', 'gamedata' ]) {
        const from = path.join(root, folder);

        if (existsSync(from)) cpSync(from, path.join(dist, folder), { recursive: true });
    }
};

/** Writing the manifest is what tells a hotel in Development to reload the plugin (with its module's new hash). */
const writeManifest = () => writeFileSync(path.join(dist, 'manifest.json'), readFileSync(path.join(root, 'manifest.json')));

/**
 * The server half as the hotel takes it: its .cs files in dist/<key>/server/ (the manifest's
 * Sources: every .cs under server/). Never bin/ or obj/: a plugin folder holds no assembly, and
 * the hotel refuses one that does.
 */
const copySources = () => {
    const from = path.join(root, 'server');
    const to = path.join(dist, 'server');

    rmSync(to, { recursive: true, force: true });
    cpSync(from, to, {
        recursive: true,
        filter: source => !/[\\/](bin|obj)$/.test(source) && (statSync(source).isDirectory() || source.endsWith('.cs')),
    });
    log('server', 'sources copied');
};

/** Compiles the server sources as the hotel does; the errors, as server/File.cs(line,column), when they don't. */
const checkServer = async () => {
    const start = performance.now();
    const result = await run('dotnet', [ 'build', serverProject, '-nologo', '-v', 'q', '-clp:NoSummary;ErrorsOnly' ], { quiet: true });

    if (!result.ok) {
        log('server', `does not compile (${since(start)})\n${result.output.trim()}`);

        return false;
    }

    log('server', `compiles (${since(start)})`);

    return true;
};

/** The client module's sources, as the hotel builds them: client/src/ copied whole (a deleted file goes too). */
const copyClient = () => {
    const to = path.join(dist, 'client', 'src');

    rmSync(to, { recursive: true, force: true });
    cpSync(path.join(root, 'client', 'src'), to, { recursive: true });
};

/** A zip of dist/<key>/ with manifest.json at its root (stored deflated, no dependencies). */
const zip = (folder, file) => {
    const files = [];
    const walk = (dir) => {
        for (const name of readdirSync(dir)) {
            const full = path.join(dir, name);

            if (statSync(full).isDirectory()) walk(full);
            else files.push(full);
        }
    };

    walk(folder);

    const local = [];
    const central = [];
    let offset = 0;

    for (const full of files) {
        const name = Buffer.from(path.relative(folder, full).split(path.sep).join('/'));
        const data = readFileSync(full);
        const packed = deflateRawSync(data);
        const header = Buffer.alloc(30);

        header.writeUInt32LE(0x04034b50, 0);
        header.writeUInt16LE(20, 4);
        header.writeUInt16LE(8, 8);
        header.writeUInt32LE(crc32(data), 14);
        header.writeUInt32LE(packed.length, 18);
        header.writeUInt32LE(data.length, 22);
        header.writeUInt16LE(name.length, 26);

        const entry = Buffer.alloc(46);

        entry.writeUInt32LE(0x02014b50, 0);
        entry.writeUInt16LE(20, 4);
        entry.writeUInt16LE(20, 6);
        entry.writeUInt16LE(8, 10);
        entry.writeUInt32LE(crc32(data), 16);
        entry.writeUInt32LE(packed.length, 20);
        entry.writeUInt32LE(data.length, 24);
        entry.writeUInt16LE(name.length, 28);
        entry.writeUInt32LE(offset, 42);

        local.push(header, name, packed);
        central.push(entry, name);
        offset += header.length + name.length + packed.length;
    }

    const directory = Buffer.concat(central);
    const end = Buffer.alloc(22);

    end.writeUInt32LE(0x06054b50, 0);
    end.writeUInt16LE(files.length, 8);
    end.writeUInt16LE(files.length, 10);
    end.writeUInt32LE(directory.length, 12);
    end.writeUInt32LE(offset, 16);
    writeFileSync(file, Buffer.concat([ ...local, directory, end ]));
};

/** Calls `task` once per burst of changes, and once more if changes came in while it ran. */
const debounced = (task, ms = 150) => {
    let timer;
    let running = false;
    let again = false;
    const fire = async () => {
        if (running) {
            again = true;

            return;
        }

        running = true;

        try {
            await task();
        }
        catch (error) {
            log('dev', String(error));
        }

        running = false;

        if (again) {
            again = false;
            await fire();
        }
    };

    return () => {
        clearTimeout(timer);
        timer = setTimeout(fire, ms);
    };
};

const dev = async () => {
    const recopyClient = debounced(async () => {
        copyClient();
        log('client', 'copied; the hotel builds the module and sends clients its new version');
    });

    watch(path.join(root, 'client', 'src'), { recursive: true }, recopyClient);

    // The hotel compiles the sources it is given: a copy is all a change needs.
    const rebuildServer = debounced(async () => {
        copySources();
        writeManifest();
    });
    const recopy = debounced(async () => {
        copyFiles();
        writeManifest();
        log('files', 'copied');
    });

    watch(path.join(root, 'server'), { recursive: true }, (_event, file) => {
        if (file && /\.cs$/.test(file) && !/[\\/](bin|obj)[\\/]/.test(file)) rebuildServer();
    });

    for (const folder of [ 'wwwroot', 'gamedata' ]) {
        if (existsSync(path.join(root, folder))) watch(path.join(root, folder), { recursive: true }, recopy);
    }

    watch(path.join(root, 'manifest.json'), recopy);
    log('dev', `watching. The plugin folder for Turbo:Plugin:DevPluginPaths: ${dist}`);
};

const main = async () => {
    // A server older than the plugin fails its build with errors about names it lacks: say so first.
    const host = checkHost();

    if (host) {
        log('server', host);

        if (mode !== 'dev') process.exit(1);
    }

    if (mode === 'check') process.exit((await checkServer()) ? 0 : 1);

    await ensurePackages();

    if (mode === 'test') {
        const serverTests = path.join(root, 'tests', `${assembly}.Tests`);
        const clientTests = path.join(root, 'client', 'tests');
        // There are none after `npm run clean-sample`, until the plugin has its own (xunit fails a
        // test project with no test in it).
        const has = (folder, pattern) => existsSync(folder) && readdirSync(folder).some(file => pattern.test(file));
        const server = has(serverTests, /\.cs$/) ? await run('dotnet', [ 'test', serverTests, '-nologo' ]) : { ok: true };
        const client = has(clientTests, /\.test\.ts$/) ? await run('node', [ '--test', 'client/tests/*.test.ts' ]) : { ok: true };

        process.exit(server.ok && client.ok ? 0 : 1);
    }

    if (release) rmSync(dist, { recursive: true, force: true });

    mkdirSync(dist, { recursive: true });
    copyFiles();
    copySources();
    copyClient();
    writeManifest();

    if (mode === 'dev') return dev();

    if (!(await checkServer())) process.exit(1);

    if (release) {
        const file = path.join(root, 'dist', `${key}-${manifest.Version}.zip`);

        zip(dist, file);
        log('package', file);
    }
    else {
        log('build', `done: ${dist}`);
    }
};

await main();
