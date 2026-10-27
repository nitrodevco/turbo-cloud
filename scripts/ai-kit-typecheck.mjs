#!/usr/bin/env node
// Type-checks every `ts` and `tsx` block of the AI kit's skills (ai/skills/*/SKILL.md) against
// nitro-next's client module SDK, as a plugin's `npm run typecheck` would (the template's
// client/tsconfig.json): code an assistant copies from a skill must type-check, as AiKitTests
// makes its C# compile.
//
//   node scripts/ai-kit-typecheck.mjs [nitro-next folder] [--node-modules <dir>]
//
// The folder defaults to $NITRO_NEXT, then ../nitro-next beside this repository; TypeScript and
// @types/react come from <dir> (default: nitro-next's node_modules).
//
// A block that imports or exports is a module of its own. Any other is a fragment of a module's
// `activate(api)`: it is checked with `api` declared, and with what the HTML comment before it
// declares (`<!-- typed with: declare const roomB: number; -->`). Exits 1 on an error, naming the
// skill's line.
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const SKILLS = join(REPO, 'ai', 'skills');
const SDK = 'packages/client-module-sdk/src';

const args = process.argv.slice(2);
const modulesAt = args.indexOf('--node-modules');
const folder = resolve(args.find((x, i) => !x.startsWith('--') && (modulesAt < 0 || i !== modulesAt + 1)) ?? process.env.NITRO_NEXT ?? join(REPO, '..', 'nitro-next'));
const modules = resolve(modulesAt >= 0 ? args[modulesAt + 1] : join(folder, 'node_modules'));
const forward = path => path.replace(/\\/g, '/');

if (!existsSync(join(folder, SDK))) {
    console.error(`No nitro-next checkout at ${folder} (${SDK} is missing): pass its folder or set NITRO_NEXT.`);
    process.exit(2);
}

for (const needed of [ 'typescript/bin/tsc', '@types/react/index.d.ts' ]) {
    if (!existsSync(join(modules, needed))) {
        console.error(`${join(modules, needed)} is missing: install typescript and @types/react there, or name a node_modules that has them (--node-modules <dir>).`);
        process.exit(2);
    }
}

const FENCE = /^```(tsx?)\n([\s\S]*?)^```$/gm;
const TYPED_WITH = /<!--\s*typed with:\s*([\s\S]*?)\s*-->\s*$/;
const PRELUDE = [ 'import type { ClientModuleApi } from \'@nitrodevco/client-module-sdk\';', 'declare const api: ClientModuleApi;' ];

const work = mkdtempSync(join(tmpdir(), 'ai-kit-ts-'));
const blocks = [];

for (const skill of readdirSync(SKILLS).sort()) {
    const file = join(SKILLS, skill, 'SKILL.md');

    if (!existsSync(file)) continue;

    const text = readFileSync(file, 'utf8').replace(/\r\n/g, '\n');

    for (const match of text.matchAll(FENCE)) {
        const [ , language, code ] = match;
        const firstLine = text.slice(0, match.index).split('\n').length + 1;
        const module = /^\s*(import|export)\s/m.test(code);
        const given = module ? [] : [ TYPED_WITH.exec(text.slice(0, match.index).trimEnd())?.[1] ?? '' ].filter(Boolean);
        const prelude = module ? [] : [ ...PRELUDE, ...given, 'export {};' ];
        const name = `${skill}.${blocks.length}.${language}`;

        writeFileSync(join(work, name), `${[ ...prelude, code ].join('\n')}`);
        blocks.push({ name, skill, firstLine, offset: prelude.length });
    }
}

const sdk = forward(join(folder, SDK));
const react = forward(join(modules, '@types', 'react'));

// The template's client/tsconfig.json, the SDK and React found where they are.
writeFileSync(join(work, 'tsconfig.json'), JSON.stringify({
    compilerOptions: {
        target: 'ES2022',
        lib: [ 'ES2022', 'DOM' ],
        module: 'ESNext',
        moduleResolution: 'bundler',
        jsx: 'react-jsx',
        strict: true,
        noEmit: true,
        skipLibCheck: true,
        allowImportingTsExtensions: true,
        types: [],
        paths: {
            '@nitrodevco/client-module-sdk': [ `${sdk}/index.ts` ],
            '@nitrodevco/client-module-sdk/theme': [ `${sdk}/theme.ts` ],
            '@nitrodevco/client-module-sdk/types': [ `${sdk}/types.d.ts` ],
            react: [ `${react}/index.d.ts` ],
            'react/jsx-runtime': [ `${react}/jsx-runtime.d.ts` ],
        },
    },
    include: [ '*.ts', '*.tsx' ],
}, null, 4));

let output = '';

try {
    execFileSync(process.execPath, [ join(modules, 'typescript', 'bin', 'tsc'), '-p', work, '--pretty', 'false' ], { encoding: 'utf8' });
} catch (error) {
    output = `${error.stdout ?? ''}${error.stderr ?? ''}`;
}

rmSync(work, { recursive: true, force: true });

// block.N.tsx(line,col): error TS... -> ai/skills/<skill>/SKILL.md:<line>:<col>: error TS...
const errors = output.split(/\r?\n/).filter(Boolean).map((line) => {
    const at = /^(?:.*[\\/])?([\w.-]+\.tsx?)\((\d+),(\d+)\): (.*)$/.exec(line);
    const block = at && blocks.find(x => x.name === at[1]);

    return block ? `ai/skills/${block.skill}/SKILL.md:${block.firstLine + Number(at[2]) - 1 - block.offset}:${at[3]}: ${at[4]}` : line;
});

if (errors.length) {
    console.error(errors.join('\n'));
    console.error(`\n${errors.filter(x => x.includes(': error TS')).length} type errors in the skills' TypeScript blocks.`);
    process.exit(1);
}

console.log(`${blocks.length} TypeScript blocks of ${new Set(blocks.map(x => x.skill)).size} skills type-check against ${forward(folder)}.`);
