// The plugin changelog, from one file per change: docs/changelog/<yyyy-mm-dd>-<slug>.md. Each pull
// request adds its own file, so two pull requests never touch the same lines. This checks every
// file and writes the entries, newest first under their date, to generated/changelog.md, which
// docs/plugin-changelog.md includes on the site.
//
//   node scripts/changelog.mjs           checks the files and writes the page's entries
//   node scripts/changelog.mjs --check   checks the files only (CI)
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const folder = resolve(here, '../../docs/changelog');
const target = resolve(here, '../generated/changelog.md');

const NAME = /^(\d{4}-\d{2}-\d{2})-([a-z0-9]+(?:-[a-z0-9]+)*)\.md$/;
const PR = /^([a-z0-9][\w.-]*)#(\d+)$/;
const LABELS = ['What', 'Breaks plugins', 'Action'];

// The frontmatter is one "key: value" a line; a value may be in double quotes (as JSON).
function frontmatter(text) {
    const block = /^---\n([\s\S]*?)\n---\n/.exec(text);
    if (!block) return null;

    const fields = {};
    for (const line of block[1].split('\n')) {
        const field = /^([a-z]+):\s*(.*)$/.exec(line);
        if (!field) throw new Error(`"${line}" is not a "key: value" line`);

        const value = field[2].trim();
        fields[field[1]] = value.startsWith('"') ? JSON.parse(value) : value;
    }

    return { fields, body: text.slice(block[0].length) };
}

// The body is labelled paragraphs: **What**: ..., **Breaks plugins**: ... (optional), **Action**: ...
function sections(body) {
    const found = {};
    let label = null;

    for (const line of body.split('\n')) {
        const start = /^\*\*(.+?)\*\*:\s*(.*)$/.exec(line);
        if (start && LABELS.includes(start[1])) {
            if (found[start[1]] !== undefined) throw new Error(`**${start[1]}** twice`);
            label = start[1];
            found[label] = [start[2]];
            continue;
        }
        if (label) found[label].push(line);
        else if (line.trim() !== '') throw new Error(`"${line}" is before **What**`);
    }

    return Object.fromEntries(Object.entries(found).map(([k, lines]) => [k, lines.join('\n').trim()]));
}

/** Every fragment, checked; the problems found, one line each. */
export function readFragments() {
    const entries = [];
    const problems = [];

    for (const name of readdirSync(folder).sort()) {
        if (name === 'README.md') continue;

        const problem = (text) => problems.push(`docs/changelog/${name}: ${text}`);
        const file = NAME.exec(name);
        if (!file) {
            problem('is not named <yyyy-mm-dd>-<slug>.md (a lower-case slug of letters, digits and dashes)');
            continue;
        }

        try {
            const parsed = frontmatter(readFileSync(resolve(folder, name), 'utf8').replace(/\r\n/g, '\n'));
            if (!parsed) {
                problem('has no frontmatter (--- title, date, pr, breaks, who ---)');
                continue;
            }

            const { fields, body } = parsed;
            const before = problems.length;

            for (const key of ['title', 'date', 'pr', 'breaks', 'who']) {
                if (!fields[key]) problem(`has no ${key}`);
            }
            for (const key of Object.keys(fields)) {
                if (!['title', 'date', 'pr', 'breaks', 'who'].includes(key)) problem(`has "${key}", which is not a field`);
            }
            if (fields.date && fields.date !== file[1]) problem(`is dated ${fields.date} but named ${file[1]}`);
            if (fields.breaks && !['yes', 'no'].includes(fields.breaks)) problem(`breaks is "${fields.breaks}", not yes or no`);

            const prs = (fields.pr ?? '').split(',').map((x) => x.trim()).filter(Boolean);
            for (const pr of prs) {
                if (!PR.test(pr)) problem(`pr "${pr}" is not <repository>#<number> (turbo-cloud#480)`);
            }

            const parts = sections(body);
            if (!parts.What) problem('has no **What**: paragraph');
            if (!parts.Action) problem('has no **Action**: paragraph');
            if (fields.breaks === 'yes' && !parts['Breaks plugins']) problem('breaks plugins but has no **Breaks plugins**: paragraph saying how');

            if (problems.length === before) {
                entries.push({ ...fields, prs, sections: parts, slug: file[2] });
            }
        } catch (e) {
            problem(e.message);
        }
    }

    return { entries, problems };
}

const indent = (text) => text.replace(/\n(?=.)/g, '\n  ');

function prLink(pr) {
    const [, repo, number] = PR.exec(pr);

    return `[${pr}](https://github.com/nitrodevco/${repo}/pull/${number})`;
}

const firstNumber = (entry) => Number(PR.exec(entry.prs[0])[2]);

/** The entries as the page shows them: newest date first, the highest pull request first within it. */
export function render(entries) {
    const sorted = [...entries].sort(
        (a, b) => b.date.localeCompare(a.date) || firstNumber(b) - firstNumber(a) || a.slug.localeCompare(b.slug),
    );
    const lines = [];
    let date = null;

    for (const entry of sorted) {
        if (entry.date !== date) {
            date = entry.date;
            lines.push(`## ${date}`, '');
        }

        const breaks = entry.sections['Breaks plugins'];
        lines.push(
            `### ${entry.title}`,
            '',
            `- **What**: ${indent(entry.sections.What)}`,
            `- **Who**: ${entry.who}`,
            `- **Breaks plugins**: ${entry.breaks}.${breaks ? ' ' + indent(breaks) : ''}`,
            `- **Action**: ${indent(entry.sections.Action)}`,
            `- ${entry.prs.map(prLink).join(',\n  ')}`,
            '',
        );
    }

    return lines.join('\n');
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    const { entries, problems } = readFragments();

    if (problems.length > 0) {
        console.error(`The plugin changelog's entries (docs/plugin-changelog.md, "Adding an entry", says how to write one):\n${problems.join('\n')}`);
        process.exit(1);
    }

    if (!process.argv.includes('--check')) {
        mkdirSync(dirname(target), { recursive: true });
        writeFileSync(target, render(entries));
    }

    console.log(`changelog: ${entries.length} entries${process.argv.includes('--check') ? ' checked' : ` -> ${target}`}`);
}
