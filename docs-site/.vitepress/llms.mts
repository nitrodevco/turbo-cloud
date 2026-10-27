import { mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, posix, relative, resolve } from 'node:path';
import type { SiteConfig } from 'vitepress';

type NavSection = { text: string; items: { text: string; link: string }[] };

function markdownFiles(dir: string, root = dir): string[] {
    return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
        const full = resolve(dir, entry.name);
        if (entry.isDirectory()) return entry.name === 'node_modules' ? [] : markdownFiles(full, root);

        return entry.name.endsWith('.md') ? [relative(root, full).split('\\').join('/')] : [];
    });
}

// A page read whole, with what it includes for the site (<!--@include: path-->: the plugin
// changelog's entries) in place.
function withIncludes(docs: string, file: string): string {
    const full = resolve(docs, file);

    return readFileSync(full, 'utf8').replace(/<!--\s*@include:\s*(.*?)\s*-->/g, (_, path: string) =>
        readFileSync(resolve(dirname(full), path), 'utf8').replace(/^---[\s\S]*?\n---\n/, ''),
    );
}

// The first paragraph under "## Introduction", or else the first paragraph after the title.
function summary(markdown: string): string {
    const body = markdown.replace(/^---[\s\S]*?\n---\n/, '');
    const intro = /\n## Introduction\s*\n+([\s\S]*?)(\n\s*\n|$)/.exec(body);
    const first = intro ?? /^# .*\n+(?:>.*\n|\s*\n)*([^#>\-|`\s][\s\S]*?)(\n\s*\n|$)/.exec(body);
    const text = first?.[1].replace(/\s+/g, ' ').trim() ?? '';
    const sentence = /^(.*?[.!?])(\s|$)/.exec(text)?.[1] ?? text;

    return sentence.length > 220 ? sentence.slice(0, 217) + '...' : sentence;
}

/**
 * Next to each page, its markdown source (<page>.md, read by "Copy as markdown"); and llms.txt (the
 * sidebar as a list of links with a line each) and llms-full.txt (every page in sidebar order).
 */
export function writeMarkdownFiles(site: SiteConfig, docs: string, nav: NavSection[], base: string) {
    for (const file of markdownFiles(docs)) {
        if (file === 'documentation.md') continue;

        const target = resolve(site.outDir, file);
        mkdirSync(dirname(target), { recursive: true });
        writeFileSync(target, withIncludes(docs, file));
    }

    const fileOf = (link: string) => {
        const path = link.split('#')[0].replace(/^\//, '');

        return path === '' ? 'index.md' : `${path}.md`;
    };

    const lines = [
        '# Turbo',
        '',
        '> Turbo is a Habbo emulator (C#, Orleans) with a plugin framework and a TypeScript client, nitro-next.',
        '',
        'Each link is the markdown source of one page of the documentation.',
    ];
    const full: string[] = [];
    const seen = new Set<string>();

    for (const section of nav) {
        lines.push('', `## ${section.text}`, '');

        for (const item of section.items) {
            if (/^https?:/.test(item.link)) {
                lines.push(`- [${item.text}](${item.link})`);
                continue;
            }

            const file = fileOf(item.link);
            const markdown = withIncludes(docs, file);
            const line = summary(markdown);
            lines.push(`- [${item.text}](${posix.join(base, file)})${line ? `: ${line}` : ''}`);

            if (!seen.has(file)) {
                seen.add(file);
                full.push(`<!-- ${file} -->\n\n${markdown.trim()}\n`);
            }
        }
    }

    writeFileSync(resolve(site.outDir, 'llms.txt'), lines.join('\n') + '\n');
    writeFileSync(resolve(site.outDir, 'llms-full.txt'), full.join('\n---\n\n'));
}
