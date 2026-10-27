import { existsSync, readFileSync } from 'node:fs';
import { posix, resolve } from 'node:path';
import { defineConfig, type DefaultTheme } from 'vitepress';
import { checkAnchors, githubSlug } from './anchors.mts';
import { repoLinks } from './repo-links.mts';
import { writeMarkdownFiles } from './llms.mts';

// The site is built from ../docs: documentation.md is the sidebar, every other page is a page.
const docs = resolve(__dirname, '../../docs');
const base = process.env.DOCS_BASE ?? '/';

type NavSection = { text: string; items: { text: string; link: string }[] };

// documentation.md is Laravel's shape: "- ## Section" then "    - [Title](page.md)" lines.
export function readNav(): NavSection[] {
    const sections: NavSection[] = [];

    for (const line of readFileSync(resolve(docs, 'documentation.md'), 'utf8').split(/\r?\n/)) {
        const section = /^- ## (.+)$/.exec(line);
        if (section) {
            sections.push({ text: section[1].trim(), items: [] });
            continue;
        }

        const page = /^\s+- \[(.+?)\]\((.+?)\)/.exec(line);
        if (page && sections.length > 0) {
            sections[sections.length - 1].items.push({ text: page[1], link: pageLink(page[2]) });
        }
    }

    return sections;
}

// nitro-next's guides are linked by their GitHub address, which works on GitHub too; the site has
// them as its own client/ pages.
const NITRO_DOCS = 'https://github.com/nitrodevco/nitro-next/blob/pixi-render/docs/';

function pageLink(target: string): string {
    if (target.startsWith(NITRO_DOCS)) target = 'client/' + target.slice(NITRO_DOCS.length);
    if (/^https?:/.test(target)) return target;

    const [path, hash] = target.split('#');
    const link = '/' + posix.normalize(path).replace(/\.md$/, '').replace(/(^|\/)index$/, '$1');

    return hash ? `${link}#${hash}` : link;
}

const nav = readNav();

// A sidebar entry for a page that does not exist fails the build, as a dead link in a page does.
for (const item of nav.flatMap((s) => s.items)) {
    const path = item.link.split('#')[0].replace(/^\//, '');
    if (/^https?:/.test(item.link) || existsSync(resolve(docs, path === '' ? 'index.md' : `${path}.md`))) continue;

    throw new Error(`documentation.md links to ${item.link}, which is not a page in docs/`);
}

const deadAnchors = checkAnchors(docs);
if (deadAnchors.length > 0) throw new Error(`Links to headings that are not there:\n${deadAnchors.join('\n')}`);

export default defineConfig({
    title: 'Turbo',
    description: 'Documentation for Turbo, the Habbo emulator, its plugins and its client.',
    base,
    srcDir: docs,
    // docs/changelog/ is the plugin changelog's entries, which plugin-changelog.md includes.
    srcExclude: ['documentation.md', 'changelog/**', '**/node_modules/**'],
    cleanUrls: true,
    // A dead link between pages fails the build; a local address in an example is not one.
    ignoreDeadLinks: 'localhostLinks',
    lastUpdated: false,
    outDir: resolve(__dirname, '../dist'),
    cacheDir: resolve(__dirname, '../node_modules/.vitepress-cache'),
    head: [['meta', { name: 'theme-color', content: '#1e7295' }]],
    markdown: {
        // The pages are GitHub markdown: text such as <name> is text, never an HTML or Vue tag.
        html: false,
        // Flash's AS3, quoted in the client's guides, reads well enough as JavaScript.
        languageAlias: { actionscript: 'js', as3: 'js' },
        anchor: { slugify: githubSlug },
        headers: { slugify: githubSlug },
        config: (md) => md.use(repoLinks, { docs }),
    },
    themeConfig: {
        nav: [
            { text: 'Installation', link: '/installation' },
            { text: 'Plugins', link: '/structure' },
            { text: 'Client', link: nav.find((s) => s.text.startsWith('Client'))?.items[0]?.link ?? '/' },
            { text: 'llms.txt', link: '/llms.txt', target: '_blank' },
        ],
        sidebar: nav.map((s) => ({ text: s.text, collapsed: false, items: s.items })) as DefaultTheme.SidebarItem[],
        search: { provider: 'local' },
        outline: { level: [2, 3], label: 'On this page' },
        editLink: {
            // Serialized into the client: it can use nothing from this file. Mirrors sourceOf.
            pattern: ({ filePath }) =>
                filePath.startsWith('client/')
                    ? `https://github.com/nitrodevco/nitro-next/edit/pixi-render/docs/${filePath.slice('client/'.length)}`
                    : `https://github.com/nitrodevco/turbo-cloud/edit/main/docs/${filePath}`,
            text: 'Edit this page on GitHub',
        },
        socialLinks: [{ icon: 'github', link: 'https://github.com/nitrodevco/turbo-cloud' }],
        footer: { message: 'Turbo is open source. Documentation changes ship in the same pull request as the code.' },
    },
    vite: {
        // The local search index holds every page; it is one large chunk on purpose.
        build: { chunkSizeWarningLimit: 4096 },
        // The pages live outside this folder, so what they compile to imports Vue as if from here:
        // resolved from above docs/, it would find no node_modules.
        plugins: [
            {
                name: 'turbo-docs-resolve-from-site',
                enforce: 'pre',
                resolveId(id, importer, options) {
                    if (!importer?.replaceAll('\\', '/').startsWith(docs.replaceAll('\\', '/')) || !/^vue(\/|$)/.test(id)) {
                        return null;
                    }

                    return this.resolve(id, resolve(__dirname, 'config.mts'), { ...options, skipSelf: true });
                },
            },
        ],
        server: { fs: { allow: [resolve(__dirname, '..'), docs] } },
    },
    async buildEnd(site) {
        writeMarkdownFiles(site, docs, nav, base);
    },
});
