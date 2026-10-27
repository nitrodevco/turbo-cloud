// Copies nitro-next's docs/ into docs/client/ (git-ignored), so the site has the client's guides
// while they stay in the client's repository, beside the client code they describe.
//
//   node scripts/client-docs.mjs                     clones nitro-next's pixi-render branch (docs/ only)
//   NITRO_NEXT_DOCS=../nitro-next/docs node ...      uses a checkout you already have
import { execFileSync } from 'node:child_process';
import { cpSync, existsSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const target = resolve(here, '../../docs/client');
const repo = 'https://github.com/nitrodevco/nitro-next';
const branch = 'pixi-render';

function clone() {
    const dir = resolve(here, '../node_modules/.nitro-next');
    rmSync(dir, { recursive: true, force: true });

    const git = (...args) => execFileSync('git', args, { stdio: ['ignore', 'ignore', 'inherit'] });
    git('clone', '--quiet', '--depth', '1', '--filter=blob:none', '--sparse', '--branch', branch, repo, dir);
    git('-C', dir, 'sparse-checkout', 'set', 'docs');

    return join(dir, 'docs');
}

const source = process.env.NITRO_NEXT_DOCS ? resolve(process.env.NITRO_NEXT_DOCS) : clone();
if (!existsSync(join(source, 'template-patches.md'))) {
    throw new Error(`${source} is not nitro-next's docs folder`);
}

rmSync(target, { recursive: true, force: true });
cpSync(source, target, { recursive: true });

// Under each title: where the page lives, so a change goes to the client's repository.
function walk(dir) {
    for (const name of readdirSync(dir)) {
        const full = join(dir, name);
        if (statSync(full).isDirectory()) {
            walk(full);
            continue;
        }
        if (!name.endsWith('.md')) continue;

        const path = 'docs/' + relative(target, full).split('\\').join('/');
        const note =
            `> [!NOTE]\n> This page is part of the client, nitro-next: ` +
            `[\`${path}\`](${repo}/blob/${branch}/${path}). Change it there.\n`;
        const text = readFileSync(full, 'utf8').replace(/^(# .*\r?\n)/, `$1\n${note}`);
        writeFileSync(full, text);
    }
}

walk(target);
console.log(`client docs: ${relative(process.cwd(), source)} -> ${relative(process.cwd(), target)}`);
