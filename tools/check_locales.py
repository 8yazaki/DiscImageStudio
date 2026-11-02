#!/usr/bin/env python3
"""Fails when the en/ja tables and the Chinese source strings drift apart.

* every string passed to L.T / L.F / {loc:T '…'} (and every built-in preset text) has an entry
* no table entry is orphaned (its key no longer exists in the source)
* placeholders ({0}, {1:N0}…) match between the key and each translation
Run from anywhere: python tools/check_locales.py   (add --list to print missing keys)
"""
import json, pathlib, re, subprocess, sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
LOCALES = ROOT / 'src' / 'DiscImageStudio.Localization' / 'Locales'
PH = re.compile(r'\{\d+(?::[^}]*)?\}')

def main():
    out = subprocess.run([sys.executable, str(ROOT / 'tools' / 'extract_locale_keys.py')],
                         check=True, capture_output=True, text=True, encoding='utf-8').stdout
    keys = json.loads(out)
    problems = 0
    for path in sorted(LOCALES.glob('*.json')):
        table = json.loads(path.read_text(encoding='utf-8'))
        missing = [k for k in keys if not table.get(k, '').strip()]
        orphan = [k for k in table if k not in set(keys)]
        badph = [k for k, v in table.items() if sorted(PH.findall(k)) != sorted(PH.findall(v))]
        for label, items in (('missing', missing), ('orphaned', orphan), ('placeholder mismatch', badph)):
            if items:
                problems += len(items)
                print(f'{path.name}: {len(items)} {label}')
                if '--list' in sys.argv:
                    for k in items: print('   ', k)
    if problems:
        print('Locale tables are out of date. Run with --list for details.')
        return 1
    print(f'Locale tables OK ({len(keys)} strings).')
    return 0

if __name__ == '__main__':
    sys.exit(main())
