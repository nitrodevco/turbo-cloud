import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, relative, resolve } from 'node:path';

/** A heading's anchor as GitHub makes it (duplicates get -1, -2...). */
export function githubSlug(heading: string): string {
    return heading
        .trim()
        .toLowerCase()
        .replace(/[^\p{L}\p{M}\p{N}\p{Pc}\- ]/gu, '')
        .replace(/ /g, '-');
}

function pages(dir: string, root: string): string[] {
    return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
        const full = resolve(dir, entry.name);
        if (entry.isDirectory()) return ['client', 'node_modules'].includes(entry.name) && dir === root ? [] : pages(full, root);

        return entry.name.endsWith('.md') ? [full] : [];
    });
}

function anchorsOf(markdown: string): Set<string> {
    const anchors = new Set<string>();
    const counts = new Map<string, number>();
    let fence = false;

    for (const line of markdown.split(/\r?\n/)) {
        if (line.startsWith('```')) fence = !fence;
        const heading = !fence && /^#{1,6} (.+?)\s*#*\s*$/.exec(line);
        if (!heading) continue;

        const slug = githubSlug(heading[1].replace(/`/g, '').replace(/\[([^\]]*)\]\([^)]*\)/g, '$1'));
        const n = counts.get(slug) ?? 0;
        counts.set(slug, n + 1);
        anchors.add(n === 0 ? slug : `${slug}-${n}`);
    }

    return anchors;
}

/**
 * Every `(#anchor)` link in a page of docs/ (its table of contents above all) names a heading of
 * that page. The site's dead link check reads pages, not anchors, so a renamed heading would
 * otherwise leave its contents entry pointing nowhere, on the site and on GitHub alike.
 */
export function checkAnchors(docs: string): string[] {
    const problems: string[] = [];

    for (const file of pages(docs, docs)) {
        const markdown = readFileSync(file, 'utf8');
        const anchors = anchorsOf(markdown);
        const text = markdown.replace(/```[\s\S]*?```/g, '');

        const name = relative(docs, file).split('\\').join('/');

        for (const match of text.matchAll(/\]\(#([^)\s]+)\)/g)) {
            if (!anchors.has(decodeURIComponent(match[1]))) problems.push(`${name}: no heading for #${match[1]}`);
        }

        // Another page's heading: page.md#anchor, when that page is one of ours.
        for (const match of text.matchAll(/\]\(([\w./-]+\.md)#([^)\s]+)\)/g)) {
            const target = resolve(dirname(file), match[1]);
            if (!target.startsWith(docs) || !existsSync(target)) continue;

            if (!anchorsOf(readFileSync(target, 'utf8')).has(decodeURIComponent(match[2]))) {
                problems.push(`${name}: ${match[1]} has no heading for #${match[2]}`);
            }
        }
    }

    return problems;
}
