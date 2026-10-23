/**
 * The plugin's one build script, run through npm (`npm run dev`, `build`, `package`, `test`):
 *
 *   dev      builds both halves into dist/<key>/ and keeps it current: a changed .cs rebuilds the
 *            server half (the hotel reloads the plugin), a changed client file rebundles the module
 *            (the hotel sends clients its new version, which they run without a page reload).
 *   build    the same once, then stops.
 *   package  a Release build in dist/<key>/ and dist/<key>-<version>.zip, the package the admin
 *            panel's plugin upload takes.
 *   test     the server tests (dotnet test) and the client tests (node --test).
 *
 * dist/<key>/ is the plugin folder a hotel loads: name it once in the hotel's
 * Turbo:Plugin:DevPluginPaths and run the server in Development.
 */
import { spawn } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, watch, writeFileSync } from 'node:fs';
import path from 'node:path';
import { crc32, deflateRawSync } from 'node:zlib';

const root = path.resolve(import.meta.dirname, '..');
const manifest = JSON.parse(readFileSync(path.join(root, 'manifest.json'), 'utf8'));
const key = manifest.Key;
const assembly = path.parse(manifest.AssemblyFile).name;
const dist = path.join(root, 'dist', key);
const serverProject = path.join(root, 'server', `${assembly}.csproj`);
const mode = process.argv[2] ?? 'dev';
const release = mode === 'package';
const configuration = release ? 'Release' : 'Debug';

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

/** The client's packages, installed the first time (the SDK comes from the path in package.json). */
const ensurePackages = async () => {
    if (existsSync(path.join(root, 'node_modules', 'esbuild'))) return;

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

const buildServer = async () => {
    const start = performance.now();
    const result = await run('dotnet', [ 'build', serverProject, '-c', configuration, '-nologo', '-v', 'q', '-clp:NoSummary;ErrorsOnly' ], { quiet: true });

    if (!result.ok) {
        log('server', `build failed (${since(start)})\n${result.output.trim()}`);

        return false;
    }

    const out = path.join(root, 'server', 'bin', configuration, 'net10.0');

    // Only the plugin's own files: the server's assemblies are the host's (Skills.csproj keeps them out).
    for (const file of readdirSync(out)) {
        if (file.startsWith(`${assembly}.`) && !file.endsWith('.runtimeconfig.json')) cpSync(path.join(out, file), path.join(dist, file));
    }

    log('server', `built in ${since(start)}`);

    return true;
};

const clientOptions = async () => {
    const { clientModuleBuildOptions } = await import('@nitrodevco/client-module-sdk/build');

    return clientModuleBuildOptions({ entry: path.join(root, 'client', 'src', 'index.tsx'), outfile: path.join(dist, 'wwwroot', `${key}.js`), minify: release });
};

const buildClient = async () => {
    const esbuild = await import('esbuild');
    const start = performance.now();

    await esbuild.build(await clientOptions());
    log('client', `built in ${since(start)}`);
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
    const esbuild = await import('esbuild');
    let clientStart = performance.now();
    const options = await clientOptions();
    const context = await esbuild.context({
        ...options,
        plugins: [ ...options.plugins, {
            name: 'plugin-reload',
            setup(build) {
                build.onStart(() => {
                    clientStart = performance.now();
                });
                build.onEnd((result) => {
                    if (result.errors.length) {
                        log('client', `build failed: ${result.errors.map(error => error.text).join('; ')}`);

                        return;
                    }

                    writeManifest();
                    log('client', `built in ${since(clientStart)}; the hotel sends clients the new module`);
                });
            },
        } ],
    });

    await context.watch();

    const rebuildServer = debounced(async () => {
        if (await buildServer()) writeManifest();
    });
    const recopy = debounced(async () => {
        copyFiles();
        writeManifest();
        log('files', 'copied');
    });

    watch(path.join(root, 'server'), { recursive: true }, (_event, file) => {
        if (file && /\.(cs|csproj)$/.test(file) && !/[\\/](bin|obj)[\\/]/.test(file)) rebuildServer();
    });

    for (const folder of [ 'wwwroot', 'gamedata' ]) {
        if (existsSync(path.join(root, folder))) watch(path.join(root, folder), { recursive: true }, recopy);
    }

    watch(path.join(root, 'manifest.json'), recopy);
    log('dev', `watching. The plugin folder for Turbo:Plugin:DevPluginPaths: ${dist}`);
};

const main = async () => {
    await ensurePackages();

    if (mode === 'test') {
        const server = await run('dotnet', [ 'test', path.join(root, 'tests', `${assembly}.Tests`), '-nologo' ]);
        const client = await run('node', [ '--test', 'client/tests/*.test.ts' ]);

        process.exit(server.ok && client.ok ? 0 : 1);
    }

    if (release) rmSync(dist, { recursive: true, force: true });

    mkdirSync(dist, { recursive: true });
    copyFiles();

    const [ server ] = await Promise.all([ buildServer(), buildClient() ]);

    writeManifest();

    if (mode === 'dev') return dev();

    if (!server) process.exit(1);

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
