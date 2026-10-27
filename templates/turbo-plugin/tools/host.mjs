/**
 * The Turbo server the plugin builds against. TurboBin is the folder holding its assemblies: the
 * TurboBin environment variable, or else the --turbo checkout's built server (Directory.Build.props
 * says the same). `npm run dev`, `build`, `package`, `test` and `make migration` check it before they
 * build, so a server older than the plugin is reported as that, not as the compiler errors it
 * causes. `node tools/host.mjs` runs the check alone.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');

/** The server project's name (server/<name>.csproj): the plugin's class and namespace prefix. */
export const projectName = () => path.parse(readdirSync(path.join(root, 'server')).find(file => file.endsWith('.csproj'))).name;

/** The --turbo checkout, as Directory.Build.props has it (TurboCloudPath overrides it). */
const checkout = () => process.env.TurboCloudPath ?? path.resolve(root, '../turbo-cloud');

export const turboBin = () => process.env.TurboBin ?? path.join(checkout(), 'Turbo.Main', 'bin', 'Debug', 'net10.0');

const rebuild = () => (process.env.TurboBin
    ? 'Build or publish that server again from a current turbo-cloud, or set TurboBin to the bin folder of the server you run.'
    : `Build that checkout again (dotnet build ${path.join(checkout(), 'Turbo.Main')}), or set the TurboBin environment variable to the bin folder of the server you run.`);

const sources = (folder) => {
    if (!existsSync(folder)) return [];

    return readdirSync(folder, { recursive: true })
        .filter(file => file.endsWith('.cs') && !/(^|[\\/])(bin|obj)[\\/]/.test(file))
        .map(file => path.join(folder, file));
};

/**
 * Why TurboBin can't build this plugin, or null when it can: no server built there, or one older
 * than the plugin's code (a Turbo namespace the plugin uses that none of the server's assemblies
 * has, as the scheduler's was to a server built before it).
 */
export const checkHost = () => {
    const bin = turboBin();
    const contracts = path.join(bin, 'Turbo.Contracts.dll');

    if (!existsSync(contracts)) return `TurboBin is ${bin}, which holds no built Turbo server (no Turbo.Contracts.dll). ${rebuild()}`;

    // A namespace is in an assembly's metadata as its name and a 0 byte, in every assembly that
    // declares or uses a type of it.
    const assemblies = readdirSync(bin).filter(file => /^Turbo\..*\.dll$/.test(file)).map(file => readFileSync(path.join(bin, file)));
    const files = [ ...sources(path.join(root, 'server')), ...sources(path.join(root, 'tests')) ].map(file => [ file, readFileSync(file, 'utf8') ]);
    // A plugin named Turbo.<something> uses its own namespaces too.
    const own = new Set(files.flatMap(([ , code ]) => [ ...code.matchAll(/^\s*namespace\s+([\w.]+)/gm) ].map(match => match[1])));
    const missing = new Map();

    for (const [ file, code ] of files) {
        for (const [ , name ] of code.matchAll(/^\s*(?:global\s+)?using\s+(?:static\s+)?(Turbo(?:\.\w+)+)\s*;/gm)) {
            if (missing.has(name) || own.has(name)) continue;

            if (!assemblies.some(assembly => assembly.includes(`${name}\0`))) missing.set(name, path.relative(root, file));
        }
    }

    if (!missing.size) return null;

    const built = statSync(contracts).mtime.toISOString().slice(0, 16).replace('T', ' ');

    return `TurboBin (${bin}, built ${built}) is older than this plugin: its assemblies have no ${[ ...missing ].map(([ name, file ]) => `${name} (${file})`).join(', ')}. ${rebuild()}`;
};

/** A line to add to a failed build's errors when one names something the server should have. */
export const buildHint = output => (/error CS(0234|0246|1061|0117)\b/.test(output)
    ? `A name the compiler can't find may be the server's: is TurboBin (${turboBin()}) the server this plugin is for? ${rebuild()}`
    : '');

/** Builds `project` with `env` and returns its errors, each once, or null when it built. */
export const buildErrors = (project, env) => {
    const result = spawnSync('dotnet', [ 'build', project, '-nologo', '-v', 'q', '-clp:NoSummary;ErrorsOnly' ], { cwd: root, env, encoding: 'utf8' });

    if (result.status === 0) return null;

    const output = `${result.stdout ?? ''}${result.stderr ?? ''}`;
    const errors = [ ...new Set(output.split(/\r?\n/).map(line => line.trim()).filter(line => /\berror\b/.test(line))) ];

    return [ ...errors, buildHint(output) ].filter(Boolean).join('\n') || output.trim();
};

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(import.meta.filename)) {
    const problem = checkHost();

    console.log(problem ?? `TurboBin ${turboBin()} builds this plugin.`);
    process.exit(problem ? 1 : 0);
}
