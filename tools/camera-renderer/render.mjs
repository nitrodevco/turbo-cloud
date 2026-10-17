// The camera renderer: draws the render data the client sends with RenderRoom / RenderRoomThumbnail
// (Flash's SpriteDataCollector output, stored by CameraPhotoStore as photos/<id>.json or
// thumbnails/<roomId>.json) into the PNG the client loads (stories.image_url_base + photos/<id>.png).
//
//   node render.mjs <in.json> <out.png> [--cache <dir>] [--furni-url <url with %libname%>] [--room-url <url with %libname%>]
//                   [--figure-url <url with %libname%>] [--figuremap-url <url>]
//                   [--effect-url <url with %libname%>] [--effectmap-url <url>]
//                   [--effect-image-url <url prefix>] [--external-url <url prefix>]... [--small]
//
// --small also writes <out>_small.png at half the size: a photo poster on a wall loads that copy
// (FurnitureExternalImageVisualization.buildThumbnailUrl), and without it the poster is blank.
//
// What is drawn, far to near (the highest z first, as the client sorts them):
//   - the planes (the first is the background over the whole viewport): each layer's texture, its
//     columns of room content assets (HabboRoomContent, the client's asset.bundles.room) laid side by
//     side and its cells down each column, or its colour alone, multiplied by the layer's colour, with
//     its masks (a door, a window) cut out, then mapped onto the plane's corners;
//   - the sprites: furniture assets looked up in the hotel's .nitro bundles (the CDN url the client's
//     asset.urls.furni gives, cached under --cache), the avatars' parts (`h_std_ch_215_2_0`: the
//     figure library the figure map names for the part, or an effect's `..._fx<id>_...` the effect
//     map names), external images (http...) under one of the --external-url prefixes, with the
//     sprite's alpha, colour tint, flipH and additive blend;
//   - then the photo lab's choices (`filters` and `zoom`, as CameraPhotoLab.renderAllEffects draws
//     them): zoomed, the middle half at twice the size; each colour matrix and composite picture at
//     its strength, in the order sent; a frame over all. A composite or frame picture is
//     <--effect-image-url><name>.png (CameraFxPreloader: <image.library.url>Habbo-Stories/).
//
// The render data is the client's, so nothing in it is trusted: an external image is fetched only
// from a prefix the hotel named (none by default), a library name is a plain file name, and the
// canvas, the images decoded and the bytes downloaded are capped, so a crafted render can neither
// reach other hosts nor run the server out of memory.
// Not drawn (a sprite whose asset is not found is skipped, the picture still comes out): a plane
// texture or mask asset the room content does not have (the plane keeps its colour), an avatar
// effect sprite outside the `fx<id>` naming, a lab effect this renderer does not know.
// Zero dependencies: a small zip reader, PNG decoder and PNG encoder over node:zlib.

import { readFileSync, writeFileSync, mkdirSync, existsSync, statSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { inflateRawSync, deflateSync, inflateSync } from 'node:zlib';

const args = process.argv.slice(2);
const positional = [];
let cacheDir = join(dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')), '.cache');
let furniUrl = 'https://images.nitrodev.co/bundled/furniture/%libname%.nitro';
let roomUrl = 'https://images.nitrodev.co/bundled/room/%libname%.nitro';
let figureUrl = 'https://images.nitrodev.co/bundled/figures/%libname%.nitro';
let figureMapUrl = 'https://images.nitrodev.co/gamedata/FigureMap.json';
let effectUrl = 'https://images.nitrodev.co/bundled/effects/%libname%.nitro';
let effectMapUrl = 'https://images.nitrodev.co/gamedata/EffectMap.json';
let effectImageUrl = 'https://images.habbo.com/c_images/Habbo-Stories/';
const externalUrls = [];
let writeSmall = false;
let frame = '';
let scenePath = '';

for (let i = 0; i < args.length; i++) {
    if (args[i] === '--cache') cacheDir = args[++i];
    else if (args[i] === '--furni-url') furniUrl = args[++i];
    else if (args[i] === '--room-url') roomUrl = args[++i];
    else if (args[i] === '--figure-url') figureUrl = args[++i];
    else if (args[i] === '--figuremap-url') figureMapUrl = args[++i];
    else if (args[i] === '--effect-url') effectUrl = args[++i];
    else if (args[i] === '--effectmap-url') effectMapUrl = args[++i];
    else if (args[i] === '--effect-image-url') effectImageUrl = args[++i];
    else if (args[i] === '--external-url') externalUrls.push(new URL(args[++i]));
    else if (args[i] === '--small') writeSmall = true;
    else if (args[i] === '--frame') frame = args[++i];
    else if (args[i] === '--scene') scenePath = args[++i];
    else positional.push(args[i]);
}

// The client's viewfinder is a few hundred pixels a side (twice that zoomed); anything larger is
// not a photo.
const MAX_CANVAS_SIDE = 2048;
const MAX_ITEMS = 5000;
const MAX_IMAGE_PIXELS = 4096 * 4096;
const MAX_BUNDLE_BYTES = 64 * 1024 * 1024;
const MAX_EXTERNAL_BYTES = 4 * 1024 * 1024;
const MAX_MAP_BYTES = 16 * 1024 * 1024;
const LIBRARY_NAME = /^[A-Za-z0-9_-]+$/;
// A figure map or effect map in the cache is fetched again after a day, so new clothes draw.
const MAP_CACHE_MS = 24 * 60 * 60 * 1000;

const [ inPath, outPath ] = positional;

if (!inPath || !outPath) {
    console.error('usage: node render.mjs <in.json> <out.png> [--cache <dir>] [--furni-url <url>] [--small] [--frame starter-room [--scene <view.png>]]');
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

/** Half the size, each pixel the average of the two-by-two block it covers. */
const halve = (width, height, rgba) => {
    const w = Math.max(1, width >> 1), h = Math.max(1, height >> 1);
    const out = new Uint8Array(w * h * 4);

    for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
            for (let c = 0; c < 4; c++) {
                let sum = 0, n = 0;

                for (let dy = 0; dy < 2; dy++) {
                    for (let dx = 0; dx < 2; dx++) {
                        const sx = x * 2 + dx, sy = y * 2 + dy;

                        if (sx < width && sy < height) { sum += rgba[(sy * width + sx) * 4 + c]; n++; }
                    }
                }

                out[(y * w + x) * 4 + c] = Math.round(sum / n);
            }
        }
    }

    return { width: w, height: h, data: out };
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
const fetchBytes = async (url, maxBytes, cacheName, maxAgeMs = Infinity) => {
    const cachePath = cacheName ? join(cacheDir, cacheName) : null;

    if (cachePath && existsSync(cachePath) && ((Date.now() - statSync(cachePath).mtimeMs) < maxAgeMs)) return readFileSync(cachePath);

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

/** A bundle (furniture, figure or effect library): its assets (name -> source, x, y, flipH) and the sheet's frames. */
const loadBundle = async (lib, url = furniUrl, kind = '') => {
    const key = `${kind}${lib}`;

    if (bundles.has(key)) return bundles.get(key);

    let bundle = null;

    try {
        const entries = readZip(await fetchBytes(url.replace('%libname%', lib), MAX_BUNDLE_BYTES, `${key}.nitro`));
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

    bundles.set(key, bundle);

    return bundle;
};

// ---------- avatars ----------

let figureLibraries = null;
let effectLibraries = null;

/** A gamedata map (FigureMap.json / EffectMap.json), or null when it cannot be had. */
const loadMap = async (url, cacheName) => {
    try {
        return JSON.parse((await fetchBytes(url, MAX_MAP_BYTES, cacheName, MAP_CACHE_MS)).toString('utf8'));
    } catch (err) {
        console.error(`map ${url}: ${err.message}`);

        return null;
    }
};

/** The figure map as `<part type>:<part id>` -> library. */
const getFigureLibraries = async () => {
    if (figureLibraries) return figureLibraries;

    figureLibraries = new Map();

    for (const library of (await loadMap(figureMapUrl, 'FigureMap.json'))?.libraries ?? []) {
        for (const part of library.parts ?? []) {
            const key = `${part.type}:${part.id}`;

            if (!figureLibraries.has(key)) figureLibraries.set(key, library.id);
        }
    }

    return figureLibraries;
};

/** The effect map as effect id -> library. */
const getEffectLibraries = async () => {
    if (effectLibraries) return effectLibraries;

    effectLibraries = new Map();

    for (const effect of (await loadMap(effectMapUrl, 'EffectMap.json'))?.effects ?? []) effectLibraries.set(String(effect.id), effect.lib);

    return effectLibraries;
};

/**
 * The library an avatar sprite's asset is in, by its name (AvatarImageCache and AvatarVisualization
 * name them `<scale>_<action>_<part type>_<part id>_<direction>_<frame>` and
 * `<scale>_<effect member>_<direction>_<frame>`): an effect's (`..._fx<id>...`) from the effect map,
 * a figure part's from the figure map; null when it is neither.
 */
const getAvatarBundle = async (name) => {
    const effect = name.match(/^s?h_[a-z]+_fx(\d+)_/);

    if (effect) {
        const lib = (await getEffectLibraries()).get(effect[1]);

        return (lib && LIBRARY_NAME.test(lib)) ? loadBundle(lib, effectUrl, 'effect_') : null;
    }

    const part = name.match(/^s?h_[a-z]+_([a-z]+)_(\d+)_\d+_\d+$/);

    if (!part) return null;

    const lib = (await getFigureLibraries()).get(`${part[1]}:${part[2]}`);

    return (lib && LIBRARY_NAME.test(lib)) ? loadBundle(lib, figureUrl, 'figure_') : null;
};

/**
 * The cut-out of one asset as RGBA, or null: flipped when the asset says so, unless `assetFlip` is
 * false (a sprite says itself whether it is drawn flipped).
 */
const getAssetImage = (bundle, assetName, assetFlip = true) => {
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
            const dx = (asset.flipH && assetFlip) ? (frame.w - 1 - x) : x;
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

// ---------- planes ----------

// RoomContentLoader's room content library: the planes' textures and masks.
const ROOM_CONTENT_LIBRARY = 'HabboRoomContent';
// A plane is never more texture columns or cells than the viewport is pixels across.
const MAX_PLANE_CELLS = MAX_CANVAS_SIDE;

/**
 * The size of a plane's texture in pixels, from its corners: RoomPlane's texture is as many pixels
 * along each side as the side is screen pixels across (a tile's edge 32 at scale 64), or down for
 * an upright side.
 */
const getPlaneTextureSize = (points) => {
    const side = (a, b) => (a.x === b.x) ? Math.abs(b.y - a.y) : Math.abs(b.x - a.x);

    return { width: Math.round(side(points[3], points[2])), height: Math.round(side(points[3], points[1])) };
};

/**
 * One plane layer's texture (RoomPlane.getDrawingDatas): its columns (`texCols`, each the asset
 * names of its cells; a column whose cells do not repeat ends in "") laid left to right until the
 * plane is full, the cells stacked from the top (from the bottom when `bottomAligned`); or the
 * plane's colour when it has no columns. Multiplied by the colour, as PlaneVisualizationLayer and
 * RoomPlane tint it. Null when no cell's asset is found.
 */
const getPlaneTexture = (plane, room, width, height) => {
    const color = Number(plane.color ?? 0);
    const tr = ((color >> 16) & 0xff) / 255, tg = ((color >> 8) & 0xff) / 255, tb = (color & 0xff) / 255;
    const texture = { width, height, data: new Uint8Array(width * height * 4) };
    const columns = (Array.isArray(plane.texCols) ? plane.texCols : []).slice(0, MAX_PLANE_CELLS).map(column => {
        const names = (Array.isArray(column?.assetNames) ? column.assetNames : []).slice(0, MAX_PLANE_CELLS).map(String);
        const repeated = names[names.length - 1] !== '';
        const cells = names.filter(name => name).map(name => (room ? getAssetImage(room, name) : null)).filter(image => image);

        return cells.length ? { cells, repeated, width: cells[0].width } : null;
    }).filter(column => column);

    if (!columns.length) {
        if (plane.texCols?.length) return null;

        for (let i = 0; i < texture.data.length; i += 4) {
            texture.data[i] = (color >> 16) & 0xff; texture.data[i + 1] = (color >> 8) & 0xff; texture.data[i + 2] = color & 0xff; texture.data[i + 3] = 255;
        }

        return texture;
    }

    const put = (image, x, y) => {
        for (let sy = 0; sy < image.height; sy++) {
            const dy = y + sy;
            if (dy < 0 || dy >= height) continue;

            for (let sx = 0; sx < image.width; sx++) {
                const dx = x + sx;
                if (dx < 0 || dx >= width) continue;

                const s = (sy * image.width + sx) * 4, d = (dy * width + dx) * 4;
                texture.data[d] = image.data[s] * tr; texture.data[d + 1] = image.data[s + 1] * tg;
                texture.data[d + 2] = image.data[s + 2] * tb; texture.data[d + 3] = image.data[s + 3];
            }
        }
    };

    for (let x = 0, index = 0; x < width; index++) {
        const column = columns[index % columns.length];

        if (plane.bottomAligned) {
            for (let y = height, cell = 0; y > 0 && (column.repeated || cell < column.cells.length); cell++) {
                const image = column.cells[column.cells.length - 1 - (cell % column.cells.length)];
                y -= image.height;
                put(image, x, y);
            }
        } else {
            for (let y = 0, cell = 0; y < height && (column.repeated || cell < column.cells.length); cell++) {
                const image = column.cells[cell % column.cells.length];
                put(image, x, y);
                y += image.height;
            }
        }

        x += Math.max(1, column.width);
    }

    return texture;
};

/**
 * Cuts a plane's masks out of its texture, as RoomPlane draws the plane under its merged masks:
 * each mask asset at its location (flipped about it), its darkness the share cut away
 * (TextureUtils.makeWhiteTransparent: alpha = 255 - the mean of the channels).
 */
const cutPlaneMasks = (texture, masks, room) => {
    for (const mask of (Array.isArray(masks) ? masks : []).slice(0, MAX_PLANE_CELLS)) {
        const image = room ? getAssetImage(room, String(mask?.name ?? '')) : null;

        if (!image) continue;

        const left = Math.round(Number(mask.location?.x ?? 0)) - (mask.flipH ? image.width : 0);
        const top = Math.round(Number(mask.location?.y ?? 0)) - (mask.flipV ? image.height : 0);

        for (let sy = 0; sy < image.height; sy++) {
            const dy = top + sy;
            if (dy < 0 || dy >= texture.height) continue;

            for (let sx = 0; sx < image.width; sx++) {
                const dx = left + sx;
                if (dx < 0 || dx >= texture.width) continue;

                const s = (((mask.flipV ? image.height - 1 - sy : sy) * image.width) + (mask.flipH ? image.width - 1 - sx : sx)) * 4;
                const luminance = image.data[s + 3] ? (image.data[s] + image.data[s + 1] + image.data[s + 2]) / 3 : 0;
                const d = (dy * texture.width + dx) * 4 + 3;

                texture.data[d] = Math.round(texture.data[d] * luminance / 255);
            }
        }
    }
};

/**
 * One texture pixel's step on screen along a plane's side: RoomPlane draws the texture unscaled,
 * a pixel across per pixel (down, on an upright side) on its 2:1 slope. The corners come in whole
 * pixels, so a slope that close to a half is taken as the half.
 */
const texelStep = (from, to) => {
    const dx = to.x - from.x, dy = to.y - from.y;

    if (!dx) return { x: 0, y: Math.sign(dy) };

    const slope = dy / Math.abs(dx), half = Math.round(slope * 2) / 2;

    return { x: Math.sign(dx), y: (Math.abs(slope - half) < 0.05) ? half : slope };
};

// Where in a screen pixel the texture is read, as measured against the client's own picture of the
// same photo (r32 planes, room 8): walls and landscapes (a plane with an upright side) at the
// pixel's centre, as RoomPlane moves their matrix half a pixel down; a textured floor and a flat
// sloped plane (a wall's top) where they matched it (0 of 25600 floor and 0 of 6000 wall top
// pixels off), as RoomPlane leaves those matrices where Flash has them.
const SAMPLE_UPRIGHT = { x: 0.5, y: 0.5 };
const SAMPLE_FLOOR = { x: -0.5, y: 0.25 };
const SAMPLE_FLAT = { x: 0, y: 0 };

/**
 * Draws a plane: its texture laid on its corners in the collector's order (3 the texture's top
 * left, 2 along its top, 1 along its left side), each screen pixel taking the texture pixel under it.
 */
const drawPlane = async (canvas, plane) => {
    const points = plane.cornerPoints.map(point => ({ x: Number(point.x), y: Number(point.y) }));
    const { width, height } = getPlaneTextureSize(points);
    const textured = Array.isArray(plane.texCols) && plane.texCols.length;
    const masked = Array.isArray(plane.masks) && plane.masks.length;

    if (!(width > 0 && height > 0) || (width * height > MAX_IMAGE_PIXELS)) {
        fillQuad(canvas, points, Number(plane.color ?? 0));

        return;
    }

    const room = (textured || masked) ? await loadBundle(ROOM_CONTENT_LIBRARY, roomUrl, 'room_') : null;
    const texture = getPlaneTexture(plane, room, width, height);

    if (!texture) { fillQuad(canvas, points, Number(plane.color ?? 0)); return; }

    cutPlaneMasks(texture, plane.masks, room);

    const origin = points[3];
    const { x: ux, y: uy } = texelStep(origin, points[2]);
    const { x: vx, y: vy } = texelStep(origin, points[1]);
    const det = ux * vy - uy * vx;
    const sample = (!ux || !vx) ? SAMPLE_UPRIGHT : (textured ? SAMPLE_FLOOR : SAMPLE_FLAT);

    if (!det) return;

    const minX = Math.max(0, Math.floor(Math.min(...points.map(p => p.x))));
    const maxX = Math.min(canvas.width - 1, Math.ceil(Math.max(...points.map(p => p.x))));
    const minY = Math.max(0, Math.floor(Math.min(...points.map(p => p.y))));
    const maxY = Math.min(canvas.height - 1, Math.ceil(Math.max(...points.map(p => p.y))));

    for (let y = minY; y <= maxY; y++) {
        for (let x = minX; x <= maxX; x++) {
            const px = x + sample.x - origin.x, py = y + sample.y - origin.y;
            const u = Math.floor((px * vy - py * vx) / det);
            const v = Math.floor((py * ux - px * uy) / det);

            if (u < 0 || u >= width || v < 0 || v >= height) continue;

            const s = (v * width + u) * 4;
            const a = texture.data[s + 3] / 255;

            if (a <= 0) continue;

            const d = (y * canvas.width + x) * 4;

            canvas.data[d] = texture.data[s] * a + canvas.data[d] * (1 - a);
            canvas.data[d + 1] = texture.data[s + 1] * a + canvas.data[d + 1] * (1 - a);
            canvas.data[d + 2] = texture.data[s + 2] * a + canvas.data[d + 2] * (1 - a);
            canvas.data[d + 3] = Math.min(255, canvas.data[d + 3] + 255 * a);
        }
    }
};

// ---------- photo lab ----------

/**
 * The lab's effects as CameraEffect.initEffects defines them (CameraEffect.as, WIN63-202610151745):
 * a colour matrix, a composite picture drawn in a blend mode, or a frame. Only these names are
 * drawn, so a render cannot make the server fetch any other picture.
 */
const CAMERA_EFFECTS = {
    dark_sepia: { type: 'colormatrix', matrix: [ 0.4, 0.4, 0.1, 0, 110, 0.3, 0.4, 0.1, 0, 30, 0.3, 0.2, 0.1, 0, 0, 0, 0, 0, 1, 0 ] },
    increase_saturation: { type: 'colormatrix', matrix: [ 2, -0.5, -0.5, 0, 0, -0.5, 2, -0.5, 0, 0, -0.5, -0.5, 2, 0, 0, 0, 0, 0, 1, 0 ] },
    increase_contrast: { type: 'colormatrix', matrix: [ 1.5, 0, 0, 0, -50, 0, 1.5, 0, 0, -50, 0, 0, 1.5, 0, -50, 0, 0, 0, 1.5, 0 ] },
    shadow_multiply_02: { type: 'composite', blendmode: 'multiply' },
    color_1: { type: 'colormatrix', matrix: [ 0.393, 0.769, 0.189, 0, 0, 0.349, 0.686, 0.168, 0, 0, 0.272, 0.534, 0.131, 0, 0, 0, 0, 0, 1, 0 ] },
    hue_bright_sat: { type: 'colormatrix', matrix: [ 1, 0.6, 0.2, 0, -50, 0.2, 1, 0.6, 0, -50, 0.6, 0.2, 1, 0, -50, 0, 0, 0, 1, 0 ] },
    hearts_hardlight_02: { type: 'composite', blendmode: 'hardlight' },
    texture_overlay: { type: 'composite', blendmode: 'overlay' },
    pinky_nrm: { type: 'composite', blendmode: 'normal' },
    color_2: { type: 'colormatrix', matrix: [ 0.333, 0.333, 0.333, 0, 0, 0.333, 0.333, 0.333, 0, 0, 0.333, 0.333, 0.333, 0, 0, 0, 0, 0, 1, 0 ] },
    night_vision: { type: 'colormatrix', matrix: [ 0, 0, 0, 0, 0, 0, 1.1, 0, 0, -50, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0 ] },
    stars_hardlight_02: { type: 'composite', blendmode: 'hardlight' },
    coffee_mpl: { type: 'composite', blendmode: 'multiply' },
    security_hardlight: { type: 'composite', blendmode: 'hardlight' },
    bluemood_mpl: { type: 'composite', blendmode: 'multiply' },
    rusty_mpl: { type: 'composite', blendmode: 'multiply' },
    decr_conrast: { type: 'colormatrix', matrix: [ 0.5, 0, 0, 0, 50, 0, 0.5, 0, 0, 50, 0, 0, 0.5, 0, 50, 0, 0, 0, 1, 0 ] },
    green_2: { type: 'colormatrix', matrix: [ 0.5, 0.5, 0.5, 0, 0, 0.5, 0.5, 0.5, 0, 90, 0.5, 0.5, 0.5, 0, 0, 0, 0, 0, 1, 0 ] },
    alien_hrd: { type: 'composite', blendmode: 'hardlight' },
    color_3: { type: 'colormatrix', matrix: [ 0.609, 0.609, 0.082, 0, 0, 0.309, 0.609, 0.082, 0, 0, 0.309, 0.609, 0.082, 0, 0, 0, 0, 0, 1, 0 ] },
    color_4: { type: 'colormatrix', matrix: [ 0.8, -0.8, 1, 0, 70, 0.8, -0.8, 1, 0, 70, 0.8, -0.8, 1, 0, 70, 0, 0, 0, 1, 0 ] },
    toxic_hrd: { type: 'composite', blendmode: 'hardlight' },
    hypersaturated: { type: 'colormatrix', matrix: [ 2, -1, 0, 0, 0, -1, 2, 0, 0, 0, 0, -1, 2, 0, 0, 0, 0, 0, 1, 0 ] },
    Yellow: { type: 'colormatrix', matrix: [ 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0 ] },
    misty_hrd: { type: 'composite', blendmode: 'hardlight' },
    x_ray: { type: 'colormatrix', matrix: [ 0, 1.2, 0, 0, -100, 0, 2, 0, 0, -120, 0, 2, 0, 0, -120, 0, 0, 0, 1, 0 ] },
    decrease_saturation: { type: 'colormatrix', matrix: [ 0.7, 0.2, 0.2, 0, 0, 0.2, 0.7, 0.2, 0, 0, 0.2, 0.2, 0.7, 0, 0, 0, 0, 0, 1, 0 ] },
    drops_mpl: { type: 'composite', blendmode: 'multiply' },
    shiny_hrd: { type: 'composite', blendmode: 'hardlight' },
    glitter_hrd: { type: 'composite', blendmode: 'hardlight' },
    frame_gold: { type: 'frame' },
    frame_gray_4: { type: 'frame' },
    frame_black_2: { type: 'frame' },
    frame_wood_2: { type: 'frame' },
    finger_nrm: { type: 'frame' },
    color_5: { type: 'colormatrix', matrix: [ 3.309, 0.609, 1.082, 0.2, 0, 0.309, 0.609, 0.082, 0, 0, 1.309, 0.609, 0.082, 0, 0, 0, 0, 0, 1, 0 ] },
    black_white_negative: { type: 'colormatrix', matrix: [ -0.5, -0.5, -0.5, 0, 255, -0.5, -0.5, -0.5, 0, 255, -0.5, -0.5, -0.5, 0, 255, 0, 0, 0, 1, 0 ] },
    blue: { type: 'colormatrix', matrix: [ 0.5, 0.5, 0.5, 0, -255, 0.5, 0.5, 0.5, 0, -170, 0.5, 0.5, 0.5, 0, 0, 0, 0, 0, 1, 0 ] },
    red: { type: 'colormatrix', matrix: [ 0.5, 0.5, 0.5, 0, 0, 0.5, 0.5, 0.5, 0, -170, 0.5, 0.5, 0.5, 0, -170, 0, 0, 0, 1, 0 ] },
    green: { type: 'colormatrix', matrix: [ 0.5, 0.5, 0.5, 0, -170, 0.5, 0.5, 0.5, 0, 0, 0.5, 0.5, 0.5, 0, -170, 0, 0, 0, 1, 0 ] },
};

const IDENTITY_MATRIX = [ 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0 ];
const MAX_EFFECTS = 64;

/** A channel as Flash keeps it: 0-255, whole. */
const clampChannel = (value) => Math.max(0, Math.min(255, Math.round(value)));

/** `renderAllEffects`'s zoom (a matrix of 2 moved back by half the size, unsmoothed): the middle half at twice the size. */
const zoomTwice = (image) => {
    const { width, height, data } = image;
    const out = new Uint8Array(data.length);

    for (let y = 0; y < height; y++) {
        const sy = Math.floor((y + (height / 2)) / 2);

        for (let x = 0; x < width; x++) {
            const sx = Math.floor((x + (width / 2)) / 2);
            const s = ((sy * width) + sx) * 4, d = ((y * width) + x) * 4;

            out[d] = data[s]; out[d + 1] = data[s + 1]; out[d + 2] = data[s + 2]; out[d + 3] = data[s + 3];
        }
    }

    return { width, height, data: out };
};

/** `getColorMatrixFilter`: the matrix mixed with the identity by the strength, over every pixel, clamped. */
const applyColorMatrix = (image, matrix, strength) => {
    const m = matrix.map((value, i) => (value * strength) + (IDENTITY_MATRIX[i] * (1 - strength)));
    const data = image.data;

    for (let i = 0; i < data.length; i += 4) {
        const r = data[i], g = data[i + 1], b = data[i + 2], a = data[i + 3];

        data[i] = clampChannel((m[0] * r) + (m[1] * g) + (m[2] * b) + (m[3] * a) + m[4]);
        data[i + 1] = clampChannel((m[5] * r) + (m[6] * g) + (m[7] * b) + (m[8] * a) + m[9]);
        data[i + 2] = clampChannel((m[10] * r) + (m[11] * g) + (m[12] * b) + (m[13] * a) + m[14]);
        data[i + 3] = clampChannel((m[15] * r) + (m[16] * g) + (m[17] * b) + (m[18] * a) + m[19]);
    }
};

/** Flash's blend modes on one channel (0-255): the source `s` over the destination `d`. */
const BLEND = {
    normal: (s) => s,
    multiply: (s, d) => (s * d) / 255,
    hardlight: (s, d) => ((s <= 127) ? ((2 * s * d) / 255) : (255 - ((2 * (255 - s) * (255 - d)) / 255))),
    overlay: (s, d) => ((d <= 127) ? ((2 * s * d) / 255) : (255 - ((2 * (255 - s) * (255 - d)) / 255))),
};

/** A composite or frame picture drawn over the photo at 0,0 in a blend mode, its alpha times `strength`. */
const drawOver = (image, picture, blendmode, strength) => {
    const blend = BLEND[blendmode] ?? BLEND.normal;

    for (let y = 0; y < Math.min(image.height, picture.height); y++) {
        for (let x = 0; x < Math.min(image.width, picture.width); x++) {
            const s = ((y * picture.width) + x) * 4, d = ((y * image.width) + x) * 4;
            const a = (picture.data[s + 3] / 255) * strength;

            if (a <= 0) continue;

            for (let c = 0; c < 3; c++) image.data[d + c] = clampChannel((blend(picture.data[s + c], image.data[d + c]) * a) + (image.data[d + c] * (1 - a)));

            image.data[d + 3] = Math.min(255, image.data[d + 3] + (255 * a));
        }
    }
};

/** The lab's zoom and effects over the drawn photo (CameraPhotoLab.renderAllEffects), in the order the client sent them. */
const applyPhotoLab = async (canvas, filters, zoom) => {
    let image = canvas;

    if (Number(zoom) === 2) image = zoomTwice(image);

    const list = Array.isArray(filters) ? filters.slice(0, MAX_EFFECTS) : [];

    for (const filter of list) {
        const name = String(filter?.name ?? '');
        const effect = Object.hasOwn(CAMERA_EFFECTS, name) ? CAMERA_EFFECTS[name] : null;

        if (!effect) {
            console.error(`effect ${name}: unknown`);
            continue;
        }

        // `getEffectDataJson`: the strength as alpha 0-255; a frame has none and is drawn whole.
        const strength = (effect.type === 'frame') ? 1 : Math.max(0, Math.min(255, Number(filter.alpha) || 0)) / 255;

        if (effect.type === 'colormatrix') {
            applyColorMatrix(image, effect.matrix, strength);
            continue;
        }

        try {
            const picture = decodePng(await fetchBytes(`${effectImageUrl}${name}.png`, MAX_EXTERNAL_BYTES, `effect_image_${name}.png`));

            drawOver(image, picture, effect.blendmode ?? 'normal', strength);
        } catch (err) {
            console.error(`effect ${name}: ${err.message}`);
        }
    }

    return image;
};

// ---------- frames ----------

/**
 * `starter-room`: the new user flow's round room picture, drawn as Habbo's own
 * (`nux_room_<type>_round.png`, measured): a 296x315 canvas holding an ellipse centred at
 * 147.5,151.5 with radii 148 x 142, its outer 10px a white ring, the room inside it, and the whole
 * shape repeated 4px lower in black at 30% as its shadow. Furni at the front that the ring cuts in
 * the lower half are drawn again whole over it, the way Habbo's pictures let a chair or a basket
 * break out of the frame. The render data is the room under a 296x284 viewfinder, the ellipse's
 * bounding box, which sits 9 down on the canvas.
 */
const STARTER_ROOM = {
    width: 296, height: 315, top: 9,
    cx: 147.5, cy: 151.5, rx: 148, ry: 142, ring: 10,
    shadowOffset: 4, shadowAlpha: 77,
};

/** Where a point is against an ellipse grown or shrunk by `inset`: below 1 inside. */
const ellipseDistance = (f, x, y, inset) => Math.hypot((x + 0.5 - f.cx) / (f.rx - inset), (y + 0.5 - f.cy) / (f.ry - inset));

/**
 * Whether a furni breaks out of the frame: it stands in the picture (its middle inside the circle),
 * reaches into the lower half, and only a little of it - a corner of a chair, the rim of a
 * basket - lies outside the circle. Something mostly outside
 * is the room going on past the frame, and stays cut.
 */
const isFrontPopOut = (f, image, x, y) => {
    if (y + image.height < f.cy + (f.ry * 0.5)) return false;

    // It stands in the picture: its middle is inside the circle.
    if (ellipseDistance(f, x + (image.width / 2), y + (image.height / 2), f.ring) > 1) return false;

    let inside = 0, outside = 0;

    for (let py = 0; py < image.height; py += 2) {
        for (let px = 0; px < image.width; px += 2) {
            if (image.data[((py * image.width) + px) * 4 + 3] < 128) continue;

            if (ellipseDistance(f, x + px, y + py, f.ring) > 1) outside++;
            else inside++;
        }
    }

    const share = outside / Math.max(1, inside + outside);

    return (share > POP_OUT_SHARE.min) && (share < POP_OUT_SHARE.max);
};

/** How much of a furni may lie outside the circle for it to break out: some, not most. */
const POP_OUT_SHARE = { min: 0.05, max: 0.35 };

const drawStarterRoomFrame = (scene, drawnSprites) => {
    const f = STARTER_ROOM;
    const out = { width: f.width, height: f.height, data: new Uint8Array(f.width * f.height * 4) };

    // The room, cut to the inside of the ring.
    for (let y = 0; y < f.height; y++) {
        for (let x = 0; x < f.width; x++) {
            const d = ellipseDistance(f, x, y, 0);

            if (d > 1) continue;

            const o = ((y * f.width) + x) * 4;

            if (ellipseDistance(f, x, y, f.ring) > 1) {
                out.data[o] = out.data[o + 1] = out.data[o + 2] = out.data[o + 3] = 255;
                continue;
            }

            const sy = y - f.top;

            if (sy < 0 || sy >= scene.height || x >= scene.width) continue;

            const i = ((sy * scene.width) + x) * 4;

            out.data[o] = scene.data[i]; out.data[o + 1] = scene.data[i + 1]; out.data[o + 2] = scene.data[i + 2]; out.data[o + 3] = 255;
        }
    }

    // The front furni the ring cut, whole again over it.
    for (const { image, x, y, options } of drawnSprites) {
        if (isFrontPopOut(f, image, x, y + f.top)) blit(out, image, x, y + f.top, options);
    }

    // The shadow: the picture's shape, 4px lower, black at 30%, under it.
    const result = new Uint8Array(out.data.length);

    for (let y = 0; y < f.height; y++) {
        for (let x = 0; x < f.width; x++) {
            const o = ((y * f.width) + x) * 4;
            const above = y - f.shadowOffset;
            const shadow = (above >= 0 && out.data[((above * f.width) + x) * 4 + 3] > 0) ? f.shadowAlpha : 0;
            const a = out.data[o + 3] / 255;
            const sa = (shadow / 255) * (1 - a);
            const alpha = a + sa;

            if (alpha <= 0) continue;

            result[o] = Math.round((out.data[o] * a) / alpha);
            result[o + 1] = Math.round((out.data[o + 1] * a) / alpha);
            result[o + 2] = Math.round((out.data[o + 2] * a) / alpha);
            result[o + 3] = Math.round(alpha * 255);
        }
    }

    return { width: f.width, height: f.height, data: result };
};

const FRAMES = { 'starter-room': drawStarterRoomFrame };

// ---------- render ----------

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
    const drawnSprites = [];

    if (frame && !FRAMES[frame]) throw new Error(`unknown frame: ${frame}`);

    for (const item of items) {
        if (item.plane) { await drawPlane(canvas, item.plane); continue; }

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
            // An avatar's part first; a furni whose library starts with h_ is looked up as furni after it.
            const avatarBundle = /^s?h_/.test(name) ? await getAvatarBundle(name) : null;

            if (avatarBundle) image = getAssetImage(avatarBundle, name, false);

            const match = image ? null : name.match(/^(.+?)_(64|32)_/);

            if (match && LIBRARY_NAME.test(match[1])) {
                const bundle = await loadBundle(match[1]);

                if (bundle) image = getAssetImage(bundle, name, false);
            }
        }

        if (!image) { skipped.push(name); continue; }

        const options = {
            alpha: sprite.alpha ?? 255,
            color: sprite.color ?? 0xffffff,
            flipH: !!sprite.flipH,
            blendMode: sprite.blendMode ?? 'normal',
        };

        // A flipped sprite is its asset's picture mirrored once, from its x on (Flash's canvas draws a
        // mirrored copy of the bitmap at the sprite's place); the asset's own flip is not added to it.
        blit(canvas, image, Math.round(sprite.x), Math.round(sprite.y), options);
        drawnSprites.push({ image, x: Math.round(sprite.x), y: Math.round(sprite.y), options });
        drawn++;
    }

    mkdirSync(dirname(outPath), { recursive: true });

    if (frame) {
        // The client's own view of the room, when it sent one: textured as the player saw it.
        let scene = canvas;

        if (scenePath) {
            const view = decodePng(readFileSync(scenePath));

            if (view.width === width && view.height === height) scene = view;
            else console.error(`scene ${view.width}x${view.height} is not the render's ${width}x${height}; drawn from the data`);
        }

        const framed = FRAMES[frame](scene, drawnSprites);

        writeFileSync(outPath, encodePng(framed.width, framed.height, framed.data));
        console.log(`${outPath}: ${framed.width}x${framed.height} (${frame}) from ${width}x${height}, ${drawn}/${sprites.length} sprites`);

        return;
    }

    const photo = await applyPhotoLab(canvas, data.filters, data.zoom);

    writeFileSync(outPath, encodePng(width, height, photo.data));

    if (writeSmall) {
        const small = halve(width, height, photo.data);

        writeFileSync(outPath.replace(/\.png$/, '') + '_small.png', encodePng(small.width, small.height, small.data));
    }
    console.log(`${outPath}: ${width}x${height}, ${planes.length} planes, ${drawn}/${sprites.length} sprites${skipped.length ? `, skipped ${skipped.join(' ')}` : ''}`);
};

render().catch(err => { console.error(err.message); process.exit(1); });
