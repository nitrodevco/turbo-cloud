// The camera renderer: draws the render data the client sends with RenderRoom / RenderRoomThumbnail
// (Flash's SpriteDataCollector output, stored by CameraPhotoStore as photos/<id>.json or
// thumbnails/<roomId>.json) into the PNG the client loads (stories.image_url_base + photos/<id>.png).
//
//   node render.mjs <in.json> <out.png> [--cache <dir>] [--furni-url <url with %libname%>]
//                   [--external-url <url prefix>]...
//
// What is drawn, far to near (the highest z first, as the client sorts them):
//   - the planes: each a flat quad in its colour (the first is the background over the whole viewport);
//   - the sprites: furniture assets looked up in the hotel's .nitro bundles (the CDN url the client's
//     asset.urls.furni gives, cached under --cache), external images (http...) under one of the
//     --external-url prefixes, with the sprite's alpha, colour tint, flipH and additive blend.
//
// The render data is the client's, so nothing in it is trusted: an external image is fetched only
// from a prefix the hotel named (none by default), a library name is a plain file name, and the
// canvas, the images decoded and the bytes downloaded are capped, so a crafted render can neither
// reach other hosts nor run the server out of memory.
// Not drawn (a sprite whose asset is not found is skipped, the picture still comes out): avatars
// (the client sends `avatar_<id>` only - the port has no figure sprite list), plane textures and
// masks (the client sends none), the lab's filters.
// Zero dependencies: a small zip reader, PNG decoder and PNG encoder over node:zlib.

import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { inflateRawSync, deflateSync, inflateSync } from 'node:zlib';

const args = process.argv.slice(2);
const positional = [];
let cacheDir = join(dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')), '.cache');
let furniUrl = 'https://images.nitrodev.co/bundled/furniture/%libname%.nitro';
const externalUrls = [];

for (let i = 0; i < args.length; i++) {
    if (args[i] === '--cache') cacheDir = args[++i];
    else if (args[i] === '--furni-url') furniUrl = args[++i];
    else if (args[i] === '--external-url') externalUrls.push(new URL(args[++i]));
    else positional.push(args[i]);
}

// The client's viewfinder is a few hundred pixels a side (twice that zoomed); anything larger is
// not a photo.
const MAX_CANVAS_SIDE = 2048;
const MAX_ITEMS = 5000;
const MAX_IMAGE_PIXELS = 4096 * 4096;
const MAX_BUNDLE_BYTES = 64 * 1024 * 1024;
const MAX_EXTERNAL_BYTES = 4 * 1024 * 1024;
const LIBRARY_NAME = /^[A-Za-z0-9_-]+$/;

const [ inPath, outPath ] = positional;

if (!inPath || !outPath) {
    console.error('usage: node render.mjs <in.json> <out.png> [--cache <dir>] [--furni-url <url>]');
    process.exit(2);
}

// ---------- PNG ----------

const crcTable = new Int32Array(256).map((_, n) => {
    let c = n;
    for (let k = 0; k < 8; k++) c = (c & 1) ? (0xedb88320 ^ (c >>> 1)) : (c >>> 1);
    return c;
});

const crc32 = (bytes) => {
    let c = -1;
    for (const b of bytes) c = crcTable[(c ^ b) & 0xff] ^ (c >>> 8);
    return (c ^ -1) >>> 0;
};

const paeth = (a, b, c) => {
    const p = a + b - c;
    const pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
    return (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
};

/** Decodes an 8-bit, non-interlaced PNG (colour types 0, 2, 3, 4, 6) into RGBA. */
const decodePng = (buf) => {
    if (buf.readUInt32BE(0) !== 0x89504e47) throw new Error('not a png');
    let pos = 8, width = 0, height = 0, colorType = 0, bitDepth = 0, interlace = 0;
    const idat = [];
    let palette = null, trns = null;

    while (pos < buf.length) {
        const length = buf.readUInt32BE(pos);
        const type = buf.toString('latin1', pos + 4, pos + 8);
        const data = buf.subarray(pos + 8, pos + 8 + length);

        if (type === 'IHDR') {
            width = data.readUInt32BE(0); height = data.readUInt32BE(4);
            bitDepth = data[8]; colorType = data[9]; interlace = data[12];
        } else if (type === 'PLTE') palette = data;
        else if (type === 'tRNS') trns = data;
        else if (type === 'IDAT') idat.push(data);
        else if (type === 'IEND') break;

        pos += 12 + length;
    }

    if (bitDepth !== 8 || interlace !== 0) throw new Error(`unsupported png: depth ${bitDepth} interlace ${interlace}`);
    if (!width || !height || width * height > MAX_IMAGE_PIXELS) throw new Error(`png too large: ${width}x${height}`);

    const channels = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 }[colorType];
    const raw = inflateSync(Buffer.concat(idat));
    const stride = width * channels;
    const out = new Uint8Array(width * height * 4);
    let prev = new Uint8Array(stride);
    let offset = 0;

    for (let y = 0; y < height; y++) {
        const filter = raw[offset++];
        const line = new Uint8Array(raw.subarray(offset, offset + stride));
        offset += stride;

        for (let i = 0; i < stride; i++) {
            const a = i >= channels ? line[i - channels] : 0;
            const b = prev[i];
            const c = i >= channels ? prev[i - channels] : 0;

            switch (filter) {
                case 1: line[i] = (line[i] + a) & 0xff; break;
                case 2: line[i] = (line[i] + b) & 0xff; break;
                case 3: line[i] = (line[i] + ((a + b) >> 1)) & 0xff; break;
                case 4: line[i] = (line[i] + paeth(a, b, c)) & 0xff; break;
            }
        }

        for (let x = 0; x < width; x++) {
            const o = (y * width + x) * 4;
            const s = x * channels;

            switch (colorType) {
                case 0: out[o] = out[o + 1] = out[o + 2] = line[s]; out[o + 3] = 255; break;
                case 2: out[o] = line[s]; out[o + 1] = line[s + 1]; out[o + 2] = line[s + 2]; out[o + 3] = 255; break;
                case 3: {
                    const p = line[s];
                    out[o] = palette[p * 3]; out[o + 1] = palette[p * 3 + 1]; out[o + 2] = palette[p * 3 + 2];
                    out[o + 3] = (trns && p < trns.length) ? trns[p] : 255;
                    break;
                }
                case 4: out[o] = out[o + 1] = out[o + 2] = line[s]; out[o + 3] = line[s + 1]; break;
                case 6: out[o] = line[s]; out[o + 1] = line[s + 1]; out[o + 2] = line[s + 2]; out[o + 3] = line[s + 3]; break;
            }
        }

        prev = line;
    }

    return { width, height, data: out };
};

const encodePng = (width, height, rgba) => {
    const raw = Buffer.alloc((width * 4 + 1) * height);

    for (let y = 0; y < height; y++) {
        raw[y * (width * 4 + 1)] = 0;
        Buffer.from(rgba.buffer, rgba.byteOffset + y * width * 4, width * 4).copy(raw, y * (width * 4 + 1) + 1);
    }

    const chunk = (type, data) => {
        const head = Buffer.alloc(4); head.writeUInt32BE(data.length);
        const body = Buffer.concat([ Buffer.from(type, 'latin1'), data ]);
        const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(body));
        return Buffer.concat([ head, body, crc ]);
    };

    const ihdr = Buffer.alloc(13);
    ihdr.writeUInt32BE(width, 0); ihdr.writeUInt32BE(height, 4);
    ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;

    return Buffer.concat([
        Buffer.from([ 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a ]),
        chunk('IHDR', ihdr),
        chunk('IDAT', deflateSync(raw)),
        chunk('IEND', Buffer.alloc(0)),
    ]);
};

// ---------- zip (.nitro) ----------

const readZip = (buf) => {
    let end = -1;
    for (let i = buf.length - 22; i >= Math.max(0, buf.length - 22 - 0xffff); i--) {
        if (buf.readUInt32LE(i) === 0x06054b50) { end = i; break; }
    }
    if (end < 0) throw new Error('not a zip');

    const count = buf.readUInt16LE(end + 10);
    let offset = buf.readUInt32LE(end + 16);
    const entries = {};

    for (let i = 0; i < count; i++) {
        if (buf.readUInt32LE(offset) !== 0x02014b50) break;
        const method = buf.readUInt16LE(offset + 10);
        const compressedSize = buf.readUInt32LE(offset + 20);
        const nameLength = buf.readUInt16LE(offset + 28);
        const extraLength = buf.readUInt16LE(offset + 30);
        const commentLength = buf.readUInt16LE(offset + 32);
        const localOffset = buf.readUInt32LE(offset + 42);
        const name = buf.toString('utf8', offset + 46, offset + 46 + nameLength);
        const localNameLength = buf.readUInt16LE(localOffset + 26);
        const localExtraLength = buf.readUInt16LE(localOffset + 28);
        const start = localOffset + 30 + localNameLength + localExtraLength;
        const bytes = buf.subarray(start, start + compressedSize);

        entries[name] = method === 8 ? inflateRawSync(bytes) : bytes;
        offset += 46 + nameLength + extraLength + commentLength;
    }

    return entries;
};

// ---------- assets ----------

const bundles = new Map();

/** The body of a GET, refused past maxBytes; redirects are refused, so an allowed host cannot point elsewhere. */
const fetchBytes = async (url, maxBytes, cacheName) => {
    const cachePath = cacheName ? join(cacheDir, cacheName) : null;

    if (cachePath && existsSync(cachePath)) return readFileSync(cachePath);

    const response = await fetch(url, { redirect: 'error' });

    if (!response.ok) throw new Error(`${response.status} ${url}`);
    if (Number(response.headers.get('content-length') ?? 0) > maxBytes) throw new Error(`too large: ${url}`);

    const chunks = [];
    let total = 0;

    for await (const chunk of response.body) {
        total += chunk.length;
        if (total > maxBytes) throw new Error(`too large: ${url}`);
        chunks.push(chunk);
    }

    const bytes = Buffer.concat(chunks);

    if (cachePath) {
        mkdirSync(cacheDir, { recursive: true });
        writeFileSync(cachePath, bytes);
    }

    return bytes;
};

/** A furniture bundle: its assets (name -> source, x, y, flipH) and the sheet's frames. */
const loadBundle = async (lib) => {
    if (bundles.has(lib)) return bundles.get(lib);

    let bundle = null;

    try {
        const entries = readZip(await fetchBytes(furniUrl.replace('%libname%', lib), MAX_BUNDLE_BYTES, `${lib}.nitro`));
        let assets = {}, frames = {}, image = null;

        for (const [ name, bytes ] of Object.entries(entries)) {
            if (name.endsWith('_spritesheet.json')) {
                const sheet = JSON.parse(bytes.toString('utf8'));
                frames = sheet.frames ?? {};
                image = decodePng(entries[sheet.meta?.image]);
            } else if (name.endsWith('.json')) {
                // The asset list is an array of { name, source?, x, y, flipH? } (a map in older bundles).
                const list = JSON.parse(bytes.toString('utf8')).assets ?? [];
                assets = Array.isArray(list) ? Object.fromEntries(list.map(asset => [ asset.name, asset ])) : list;
            }
        }

        bundle = image ? { assets, frames, image } : null;
    } catch (err) {
        console.error(`bundle ${lib}: ${err.message}`);
    }

    bundles.set(lib, bundle);

    return bundle;
};

/** The cut-out of one asset as RGBA (already flipped when the asset says so), or null. */
const getAssetImage = (bundle, assetName) => {
    const asset = bundle.assets[assetName];

    if (!asset) return null;

    const source = asset.source ?? assetName;
    const frame = bundle.frames[source]?.frame ?? bundle.frames[`${source}.png`]?.frame;

    if (!frame) return null;

    const { width, height, data } = bundle.image;
    const out = new Uint8Array(frame.w * frame.h * 4);

    for (let y = 0; y < frame.h; y++) {
        for (let x = 0; x < frame.w; x++) {
            const s = ((frame.y + y) * width + frame.x + x) * 4;
            const dx = asset.flipH ? (frame.w - 1 - x) : x;
            const d = (y * frame.w + dx) * 4;
            out[d] = data[s]; out[d + 1] = data[s + 1]; out[d + 2] = data[s + 2]; out[d + 3] = data[s + 3];
        }
    }

    return { width: frame.w, height: frame.h, data: out };
};

/** The url of an external image when it lies under one of the --external-url prefixes, else null. */
const allowedExternalUrl = (name) => {
    let url;

    try {
        url = new URL(name);
    } catch {
        return null;
    }

    if (url.username || url.password) return null;

    const allowed = externalUrls.some(prefix =>
        url.protocol === prefix.protocol && url.host === prefix.host && url.pathname.startsWith(prefix.pathname));

    return allowed ? url : null;
};

// ---------- drawing ----------

const blit = (canvas, image, x, y, { alpha = 255, color = 0xffffff, flipH = false, blendMode = 'normal' } = {}) => {
    const tr = ((color >> 16) & 0xff) / 255, tg = ((color >> 8) & 0xff) / 255, tb = (color & 0xff) / 255;
    const add = blendMode === 'add';

    for (let sy = 0; sy < image.height; sy++) {
        const dy = y + sy;
        if (dy < 0 || dy >= canvas.height) continue;

        for (let sx = 0; sx < image.width; sx++) {
            const dx = x + (flipH ? (image.width - 1 - sx) : sx);
            if (dx < 0 || dx >= canvas.width) continue;

            const s = (sy * image.width + sx) * 4;
            const a = (image.data[s + 3] / 255) * (alpha / 255);
            if (a <= 0) continue;

            const d = (dy * canvas.width + dx) * 4;
            const r = image.data[s] * tr, g = image.data[s + 1] * tg, b = image.data[s + 2] * tb;

            if (add) {
                canvas.data[d] = Math.min(255, canvas.data[d] + r * a);
                canvas.data[d + 1] = Math.min(255, canvas.data[d + 1] + g * a);
                canvas.data[d + 2] = Math.min(255, canvas.data[d + 2] + b * a);
            } else {
                canvas.data[d] = r * a + canvas.data[d] * (1 - a);
                canvas.data[d + 1] = g * a + canvas.data[d + 1] * (1 - a);
                canvas.data[d + 2] = b * a + canvas.data[d + 2] * (1 - a);
            }

            canvas.data[d + 3] = Math.min(255, canvas.data[d + 3] + 255 * a);
        }
    }
};

/** Fills a convex quad given in the collector's corner order (0-1 one edge, 2-3 the opposite). */
const fillQuad = (canvas, points, color) => {
    const poly = [ points[0], points[1], points[3], points[2] ];
    const minY = Math.max(0, Math.floor(Math.min(...poly.map(p => p.y))));
    const maxY = Math.min(canvas.height - 1, Math.ceil(Math.max(...poly.map(p => p.y))));
    const r = (color >> 16) & 0xff, g = (color >> 8) & 0xff, b = color & 0xff;

    for (let y = minY; y <= maxY; y++) {
        const sy = y + 0.5;
        const xs = [];

        for (let i = 0; i < 4; i++) {
            const a = poly[i], c = poly[(i + 1) % 4];
            if ((sy >= a.y && sy < c.y) || (sy >= c.y && sy < a.y)) xs.push(a.x + (sy - a.y) * (c.x - a.x) / (c.y - a.y));
        }

        if (xs.length < 2) continue;
        xs.sort((p, q) => p - q);

        for (let x = Math.max(0, Math.round(xs[0])); x < Math.min(canvas.width, Math.round(xs[xs.length - 1])); x++) {
            const d = (y * canvas.width + x) * 4;
            canvas.data[d] = r; canvas.data[d + 1] = g; canvas.data[d + 2] = b; canvas.data[d + 3] = 255;
        }
    }
};

const render = async () => {
    const data = JSON.parse(readFileSync(inPath, 'utf8'));
    const planes = [ ...(data.planes ?? []) ];
    const sprites = [ ...(data.sprites ?? []) ];
    const background = planes[0];

    if (!background?.cornerPoints?.length) throw new Error('no background plane');
    if (planes.length + sprites.length > MAX_ITEMS) throw new Error(`too many items: ${planes.length + sprites.length}`);

    const width = Math.round(Math.max(...background.cornerPoints.map(p => Number(p.x))));
    const height = Math.round(Math.max(...background.cornerPoints.map(p => Number(p.y))));

    if (!(width > 0 && width <= MAX_CANVAS_SIDE && height > 0 && height <= MAX_CANVAS_SIDE)) {
        throw new Error(`canvas out of range: ${width}x${height}`);
    }
    const canvas = { width, height, data: new Uint8Array(width * height * 4) };
    const items = [
        ...planes.map(plane => ({ z: plane.z, plane })),
        ...sprites.map(sprite => ({ z: sprite.z, sprite })),
    ].sort((a, b) => b.z - a.z);
    let drawn = 0, skipped = [];

    for (const item of items) {
        if (item.plane) { fillQuad(canvas, item.plane.cornerPoints, item.plane.color ?? 0); continue; }

        const sprite = item.sprite;
        const name = String(sprite.name ?? '');
        let image = null;

        if (name.startsWith('http')) {
            const url = allowedExternalUrl(name);

            try {
                if (url) image = decodePng(await fetchBytes(url, MAX_EXTERNAL_BYTES));
            } catch (err) {
                console.error(`external ${name}: ${err.message}`);
            }
        } else {
            const match = name.match(/^(.+?)_(64|32)_/);

            if (match && LIBRARY_NAME.test(match[1])) {
                const bundle = await loadBundle(match[1]);

                if (bundle) image = getAssetImage(bundle, name);
            }
        }

        if (!image) { skipped.push(name); continue; }

        blit(canvas, image, Math.round(sprite.x), Math.round(sprite.y), {
            alpha: sprite.alpha ?? 255,
            color: sprite.color ?? 0xffffff,
            flipH: !!sprite.flipH,
            blendMode: sprite.blendMode ?? 'normal',
        });
        drawn++;
    }

    mkdirSync(dirname(outPath), { recursive: true });
    writeFileSync(outPath, encodePng(width, height, canvas.data));
    console.log(`${outPath}: ${width}x${height}, ${planes.length} planes, ${drawn}/${sprites.length} sprites${skipped.length ? `, skipped ${skipped.join(' ')}` : ''}`);
};

render().catch(err => { console.error(err.message); process.exit(1); });
