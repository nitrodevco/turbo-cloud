import { existsSync } from 'node:fs';
import { posix, resolve } from 'node:path';
import type MarkdownIt from 'markdown-it';

// Pages under client/ are nitro-next's docs/, copied in by scripts/client-docs.mjs.
const REPOS = {
    turbo: { repo: 'nitrodevco/turbo-cloud', branch: 'main' },
    nitro: { repo: 'nitrodevco/nitro-next', branch: 'pixi-render' },
} as const;

type RepoName = keyof typeof REPOS;

export function sourceOf(relativePath: string) {
    const name: RepoName = relativePath.startsWith('client/') ? 'nitro' : 'turbo';
    const path = 'docs/' + (name === 'nitro' ? relativePath.slice('client/'.length) : relativePath);
    const { repo, branch } = REPOS[name];

    return {
        name,
        path,
        editUrl: `https://github.com/${repo}/edit/${branch}/${path}`,
        blobUrl: `https://github.com/${repo}/blob/${branch}/${path}`,
    };
}

function blob(name: RepoName, repoPath: string, hash: string) {
    const { repo, branch } = REPOS[name];

    return `https://github.com/${repo}/blob/${branch}/${repoPath}${hash}`;
}

// The site page for a path in a repo, or null when the site has no such page.
function sitePage(docs: string, name: RepoName, repoPath: string): string | null {
    if (!repoPath.startsWith('docs/') || !repoPath.endsWith('.md')) return null;

    const page = (name === 'nitro' ? 'client/' : '') + repoPath.slice('docs/'.length);

    return existsSync(resolve(docs, page)) && page !== 'documentation.md' ? page : null;
}

/**
 * Links that leave docs/ (source files, scripts, the other repo, a JSON example) go to GitHub, so
 * the site has no dead link and the page reads the same on GitHub. Inline code naming a page
 * (`docs/settings.md`, nitro-next `docs/template-patches.md`) becomes a link to it.
 */
export function repoLinks(md: MarkdownIt, { docs }: { docs: string }) {
    md.core.ruler.push('turbo_repo_links', (state) => {
        const relativePath: string | undefined = state.env?.relativePath;
        if (!relativePath) return;

        const here = sourceOf(relativePath);
        const relativeTo = (page: string) => {
            const rel = posix.relative(posix.dirname(relativePath), page);

            return rel.startsWith('.') ? rel : './' + rel;
        };

        for (const block of state.tokens) {
            if (block.type !== 'inline' || !block.children) continue;

            const children = block.children;
            for (let i = 0; i < children.length; i++) {
                const token = children[i];

                if (token.type === 'link_open') {
                    const href = token.attrGet('href') ?? '';
                    const nitro = /^https:\/\/github\.com\/nitrodevco\/nitro-next\/blob\/pixi-render\/(docs\/[^#]+\.md)(#.*)?$/.exec(href);
                    const nitroPage = nitro && sitePage(docs, 'nitro', nitro[1]);
                    if (nitroPage) {
                        token.attrSet('href', relativeTo(nitroPage) + (nitro[2] ?? ''));
                        continue;
                    }

                    if (!href || /^([a-z]+:|#|\/)/i.test(href)) continue;

                    const [path, hash = ''] = href.split(/(?=#)/);
                    const repoPath = posix.normalize(posix.join(posix.dirname(here.path), decodeURI(path)));
                    if (repoPath.startsWith('..')) continue;

                    // A page of ours that does not exist stays as written, so the build reports it
                    // dead; one of the client's guides is the client's to fix, and goes to GitHub.
                    const ourPage =
                        here.name === 'turbo' &&
                        repoPath.startsWith('docs/') &&
                        repoPath.endsWith('.md') &&
                        repoPath !== 'docs/documentation.md';
                    if (!ourPage && !sitePage(docs, here.name, repoPath)) token.attrSet('href', blob(here.name, repoPath, hash));
                    continue;
                }

                if (token.type !== 'code_inline' || children[i - 1]?.type === 'link_open') continue;

                const named = /^docs\/([\w./-]+\.md)$/.exec(token.content);
                if (!named) continue;

                const before = children[i - 1]?.type === 'text' ? children[i - 1].content : '';
                const name: RepoName = /nitro-next\s*$/.test(before)
                    ? 'nitro'
                    : /turbo-cloud\s*$/.test(before)
                      ? 'turbo'
                      : here.name;
                const page = sitePage(docs, name, token.content);
                const href = page ? relativeTo(page) : blob(name, token.content, '');
                if (!page && !existsSync(resolve(docs, '..', token.content)) && name === 'turbo') continue;

                const open = new state.Token('link_open', 'a', 1);
                open.attrSet('href', href);
                const close = new state.Token('link_close', 'a', -1);
                children.splice(i, 1, open, token, close);
                i += 2;
            }
        }
    });
}
