"""Which packets does nitro-next USE that the server does not implement?
Outgoing (client -> server): `new XComposer(` in nitro-react / nitro-renderer  vs  server XMessageHandler.
Incoming (server -> client): `on(XMessage` / subscribe in nitro-react handlers + renderer  vs  server composer use.

Usage: python scripts/packetgap.py [DomainPrefix ...]      e.g.  python scripts/packetgap.py Room
A handler counts as a stub when it reaches no grain, service or provider and sends nothing; a
composer counts as unsent when nothing outside the revision constructs it. Both are heuristics:
read the file before trusting a line of the report."""
import io
import os
import re
import sys

# The server is the repository this script lives in; nitro-next is wherever NITRO_NEXT says.
SERVER = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NITRO = os.path.join(os.environ.get('NITRO_NEXT', r'D:\Repositories\nitro-next'), 'packages')


def walk(root, exts):
    for d, dirs, fs in os.walk(root):
        dirs[:] = [x for x in dirs if x not in ('node_modules', 'dist', 'obj', 'bin', '.git')]
        for f in fs:
            if f.endswith(exts):
                yield os.path.join(d, f)


def read(p):
    return io.open(p, encoding='utf-8', errors='ignore').read()


# ---- nitro packet catalogue: class -> domain folder
out_domain, in_domain = {}, {}
for p in walk(os.path.join(NITRO, 'nitro-packets', 'src', 'outgoing'), ('.ts',)):
    name = os.path.basename(p)[:-3]
    if name != 'index':
        out_domain[name] = os.path.relpath(os.path.dirname(p), os.path.join(NITRO, 'nitro-packets', 'src', 'outgoing')).replace('\\', '/')
for p in walk(os.path.join(NITRO, 'nitro-packets', 'src', 'incoming'), ('.ts',)):
    name = os.path.basename(p)[:-3]
    if name != 'index' and '/Data' not in p.replace('\\', '/'):
        in_domain[name] = os.path.relpath(os.path.dirname(p), os.path.join(NITRO, 'nitro-packets', 'src', 'incoming')).replace('\\', '/')

# ---- what nitro-next uses
used_out, used_in = {}, {}
for pkg in ('nitro-react', 'nitro-renderer'):
    for p in walk(os.path.join(NITRO, pkg, 'src'), ('.ts', '.tsx')):
        s = read(p)
        rel = os.path.relpath(p, NITRO).replace('\\', '/')
        for m in re.finditer(r'new ([A-Z]\w+Composer)\(', s):
            if m.group(1) in out_domain:
                used_out.setdefault(m.group(1), rel)
        for m in re.finditer(r'\b([A-Z]\w+Message)\b', s):
            if m.group(1) in in_domain:
                used_in.setdefault(m.group(1), rel)

# ---- server side
handlers = {}
for p in walk(os.path.join(SERVER, 'Turbo.PacketHandlers'), ('.cs',)):
    handlers[os.path.basename(p)[:-3]] = p
composers = {}
for p in walk(os.path.join(SERVER, 'Turbo.Primitives', 'Messages', 'Outgoing'), ('.cs',)):
    composers[os.path.basename(p)[:-3]] = p
server_src = []
for proj in os.listdir(SERVER):
    if proj.startswith('Turbo.') and proj not in ('Turbo.Revisions', 'Turbo.Primitives'):
        for p in walk(os.path.join(SERVER, proj), ('.cs',)):
            server_src.append(read(p))
server_blob = '\n'.join(server_src)


def handler_status(composer):
    base = composer[:-len('Composer')]
    for cand in (base + 'MessageHandler', base + 'Handler'):
        if cand in handlers:
            body = read(handlers[cand])
            real = re.search(r'_grainFactory|grainFactory\.|Service\.|_\w+Service|ctx\.SendComposerAsync|_\w+Provider', body)
            return ('ok' if real else 'STUB'), cand
    return 'MISSING', base + 'MessageHandler'


def composer_status(message):
    base = message[:-len('Message')]
    for cand in (base + 'MessageComposer', base + 'EventMessageComposer', base + 'Composer'):
        if cand in composers:
            used = re.search(r'new ' + re.escape(cand) + r'\b', server_blob) is not None
            body = read(composers[cand])
            empty = 'TODO' in body or not re.search(r'\[Id\(', body)
            return ('ok' if used else ('UNSENT-EMPTY' if empty else 'UNSENT')), cand
    return 'MISSING', base + 'MessageComposer'


want = sys.argv[1:] or None


def wanted(domain):
    return want is None or any(domain.lower().startswith(w.lower()) for w in want)


print('=== OUTGOING (client -> server) used by nitro-next, not implemented on the server')
rows = []
for c, where in sorted(used_out.items()):
    d = out_domain[c]
    if not wanted(d):
        continue
    st, name = handler_status(c)
    if st != 'ok':
        rows.append((d, c, st, where))
for r in sorted(rows):
    print('%-28s %-52s %-8s %s' % r)
print(len(rows), 'of', sum(1 for c in used_out if wanted(out_domain[c])))

print()
print('=== INCOMING (server -> client) listened to by nitro-next, never sent by the server')
rows = []
for m, where in sorted(used_in.items()):
    d = in_domain[m]
    if not wanted(d):
        continue
    st, name = composer_status(m)
    if st != 'ok':
        rows.append((d, m, st, where))
for r in sorted(rows):
    print('%-28s %-52s %-13s %s' % r)
print(len(rows), 'of', sum(1 for m in used_in if wanted(in_domain[m])))
