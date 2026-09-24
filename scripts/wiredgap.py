"""Which wired boxes does the Flash client have an editor for that the server has no logic for?
Per kind (trigger, action, condition, selector, addon, variable) the client's editor classes each
return a code from the kind's code table; the server's boxes each return `WiredCode => (int)Enum.X`.
The report is the codes on the first list and not on the second, plus any code two server boxes
both claim.

Usage: python scripts/wiredgap.py
An editor class carries the code of its negative twin as well (`negativeCode`), so one class can
stand for two boxes; both are counted. The client has no furni names, only codes: which furni a
missing code belongs to is found in the hotel's furniture_definitions, not here."""
import glob
import io
import os
import re

# The server is the repository this script lives in; the deobfuscated client is wherever
# HABBO_CLIENT says (the folder that holds com/sulake/habbo).
SERVER = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CLIENT = os.path.join(
    os.environ.get('HABBO_CLIENT', r'D:\Habbo\WIN63-202609091217-117204808\scripts-deob'),
    'com', 'sulake', 'habbo', 'roomevents', 'wired_setup',
)
BOXES = os.path.join(SERVER, 'Turbo.Rooms', 'Object', 'Logic', 'Furniture', 'Floor', 'Wired')
ENUMS = os.path.join(SERVER, 'Turbo.Primitives', 'Rooms', 'Enums', 'Wired')

# kind: (client folder, client code table, server enum)
KINDS = {
    'trigger': ('triggerconfs', 'TriggerConfCodes', 'WiredTriggerType'),
    'action': ('actiontypes', 'ActionTypeCodes', 'WiredActionType'),
    'condition': ('conditions', 'ConditionCodes', 'WiredConditionType'),
    'selector': ('selectors', 'SelectorCodes', 'WiredSelectorType'),
    'addon': ('addons', 'AddonCodes', 'WiredAddonType'),
    'variable': ('variables', 'VariableCodes', 'WiredVariableBoxType'),
}

# A positive editor class carries the code of the box that negates it too.
CODE_GETTERS = ('code', 'negativeCode')


def read(path):
    return io.open(path, encoding='utf-8', errors='replace').read()


def client_codes(folder, table_name):
    """Every code the kind's editors declare, positive and negative, to the class declaring it."""
    table = {
        name: int(value)
        for name, value in re.findall(
            r'static var (\S+?):int = (-?\d+);',
            read(os.path.join(CLIENT, folder, table_name + '.as')),
        )
    }
    codes = {}

    for path in glob.glob(os.path.join(CLIENT, folder, '**', '*.as'), recursive=True):
        text = read(path)

        for getter in CODE_GETTERS:
            match = re.search(
                r'function get ' + getter + r'\(\) : int\s*\{\s*return ([^;]+);', text
            )

            if not match:
                continue

            named = re.match(table_name + r'\.(\S+)$', match.group(1).strip())
            code = table.get(named.group(1)) if named else None

            if code is not None:
                codes.setdefault(code, []).append(
                    (named.group(1), os.path.relpath(path, CLIENT).replace('\\', '/'))
                )

    return codes


def server_codes(enum_name):
    """Each code a box claims, to the boxes claiming it: two boxes on one code is always a bug."""
    values = {
        name: int(value)
        for name, value in re.findall(
            r'^\s*(\w+)\s*=\s*(-?\d+)', read(os.path.join(ENUMS, enum_name + '.cs')), re.M
        )
    }
    codes = {}

    for path in glob.glob(os.path.join(BOXES, '**', '*.cs'), recursive=True):
        members = re.findall(
            r'WiredCode\s*=>\s*\(int\)\s*' + enum_name + r'\.(\w+)', read(path)
        )

        for member in members:
            if member in values:
                codes.setdefault(values[member], []).append(os.path.basename(path)[:-3])

    return codes


def main():
    missing_total = 0
    clash_total = 0

    for kind, (folder, table_name, enum_name) in KINDS.items():
        codes = client_codes(folder, table_name)
        boxes = server_codes(enum_name)
        missing = sorted(set(codes) - set(boxes))
        missing_total += len(missing)

        print(f'\n== {kind}: {len(codes)} client codes, {len(missing)} without a server box')

        for code in missing:
            for name, path in codes[code]:
                print(f'   {code:5d}  {name:34s} {path}')

        for code, claimed in sorted(boxes.items()):
            if len(claimed) > 1:
                clash_total += 1

                print(f'   {code:5d}  CLAIMED BY {len(claimed)}: {", ".join(sorted(claimed))}')

    print(f'\n{missing_total} missing in all, {clash_total} code(s) claimed by more than one box')


if __name__ == '__main__':
    main()
