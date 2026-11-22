/**
 * `npm run mcp:inspect`: the official MCP Inspector (modelcontextprotocol/inspector) on the dev
 * hotel's MCP server, the one `npm run ai` wrote in .mcp.json, with its token: try the plugin's
 * tools and the dev tools by hand, see their schemas and answers as an assistant gets them.
 *
 *   npm run mcp:inspect                                     the Inspector's web page
 *   npm run mcp:inspect -- --cli --method tools/list        the tools, in the terminal
 *   npm run mcp:inspect -- --cli --method tools/call --tool-name search_docs --tool-arg query=IRoomTurn
 *
 * The token comes from the environment variable .mcp.json names (TURBO_MCP_TOKEN), never from a file.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';

const INSPECTOR = '@modelcontextprotocol/inspector@2';

const root = path.resolve(import.meta.dirname, '..');
const file = path.join(root, '.mcp.json');

const fail = (text) => {
    console.error(text);
    process.exit(1);
};

if (!existsSync(file)) fail('No .mcp.json: run npm run ai first (it names the dev hotel\'s MCP server).');

const server = JSON.parse(readFileSync(file, 'utf8')).mcpServers?.turbo;

if (!server?.url) fail('.mcp.json has no "turbo" server: run npm run ai (or npm run ai -- --mcp-url <url>).');

/** `${NAME}` in a header, from the environment; a missing variable says which. */
const headers = Object.entries(server.headers ?? {}).map(([ name, value ]) => `${name}: ${value.replace(/\$\{([A-Za-z_][A-Za-z0-9_]*)\}/g, (_, variable) => process.env[variable]
    ?? fail(`${variable} is not set. Admin panel > MCP > New token (read-only), then set ${variable}=tmcp_... and run this again.`))}`);

// The Inspector's own options first (--cli picks its client), the server last.
const args = [ '-y', INSPECTOR, ...process.argv.slice(2), '--transport', 'http', '--server-url', server.url, ...headers.flatMap(header => [ '--header', header ]) ];

console.log(`MCP Inspector on ${server.url}`);

// npx is a .cmd on Windows, started through the shell: each argument quoted there.
const windows = process.platform === 'win32';
const quote = arg => (/[\s"&|<>^]/.test(arg) ? `"${arg.replace(/"/g, '\\"')}"` : arg);
const run = spawnSync('npx', windows ? args.map(quote) : args, { cwd: root, stdio: 'inherit', shell: windows });

process.exit(run.status ?? 1);
