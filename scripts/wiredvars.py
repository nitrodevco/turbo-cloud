"""In what order does the client list the internal wired variables?

The client sorts them by variable id, and an internal id is built from the target's band, the
variable's sub-band and its order (`WiredVariableIdBuilder.CreateInternalOrdered`), highest
first. So the list this prints is the list the wired editor shows, and moving a variable means
changing its `SubBandType` or `Order` and nothing else.

Usage: python scripts/wiredvars.py
Changing either changes the variable's id, which is what a saved box stores: renumber only
when the order is wrong, and say so, because boxes referencing it lose the reference."""
import glob
import io
import os
import re

SERVER = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VARIABLES = os.path.join(SERVER, 'Turbo.Rooms', 'Wired', 'Variables')

# The sub-band byte of each name, from WiredVariableGroupSubBandType. Higher comes first.
SUB_BANDS = {'Base': 0xE0, 'Position': 0xD0, 'Meta': 0xC0, 'Other': 0x80}
DEFAULT_SUB_BAND = 'Base'
DEFAULT_ORDER = 10
MAX_INHERITANCE_DEPTH = 6


def read(path):
    return io.open(path, encoding='utf-8', errors='replace').read()


def scan(folder):
    """Every variable of one target, in the order the client lists them."""
    classes = {}

    for path in glob.glob(os.path.join(VARIABLES, folder, '*.cs')):
        text = read(path)
        flat = re.sub(r'\s+', ' ', text)
        # The declaration, not the word "class" in a comment.
        declared = re.search(r'public (?:sealed |abstract )?class (\w+)', text)

        if not declared:
            continue

        name = re.search(r'VariableName\s*=>\s*"([^"]+)"', text)
        sub_band = re.search(r'SubBandType\s*=>\s*WiredVariableGroupSubBandType\.(\w+)', flat)
        order = re.search(r'Order\s*=>\s*(\d+)', text)
        # The base constructor call is what the class derives from.
        base = re.search(r':\s*(\w+)(?:<[^>]*>)?\(roomGrain\)', text)

        classes[declared.group(1)] = {
            'name': name.group(1) if name else None,
            'sub_band': sub_band.group(1) if sub_band else None,
            'order': int(order.group(1)) if order else None,
            'base': base.group(1) if base else None,
        }

    def sub_band_of(class_name, depth=0):
        if class_name not in classes or depth > MAX_INHERITANCE_DEPTH:
            return DEFAULT_SUB_BAND

        found = classes[class_name]

        return found['sub_band'] or sub_band_of(found['base'], depth + 1)

    rows = [
        (
            sub_band_of(class_name),
            found['order'] if found['order'] is not None else DEFAULT_ORDER,
            found['name'],
        )
        for class_name, found in classes.items()
        if found['name']
    ]

    return sorted(rows, key=lambda row: (-SUB_BANDS[row[0]], -row[1]))


def main():
    for folder in sorted(
        x for x in os.listdir(VARIABLES) if os.path.isdir(os.path.join(VARIABLES, x))
    ):
        rows = scan(folder)

        if not rows:
            continue

        print(f'\n== {folder}: {len(rows)} variables, as the editor lists them')

        for position, (sub_band, order, name) in enumerate(rows, 1):
            print(f'  {position:3d}. {name:42s} {sub_band:9s} {order}')


if __name__ == '__main__':
    main()
