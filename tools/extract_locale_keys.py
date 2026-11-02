#!/usr/bin/env python3
"""Lists every translatable source string (zh-CN) used by Disc Image Studio.

Sources: L.T("…") / L.F("…") calls in .cs, {loc:T '…'} in .xaml, and displayName /
description in the built-in preset JSON. Output: JSON array on stdout (or --out file).
"""
import json, re, sys, pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
CS_CALL = re.compile(r'\bL\.(?:T|F)\(\s*(?:[A-Za-z_.]+\s*,\s*)?"((?:\\.|[^"\\])*)"')
XAML = re.compile(r"\{loc:T\s+'((?:\\.|[^'\\])*)'\}")
XAML_ENT = {'&quot;': '"', '&apos;': "'", '&lt;': '<', '&gt;': '>', '&amp;': '&', '&#10;': '\n'}

def unescape_cs(s):
    out, i = [], 0
    m = {'n': '\n', 'r': '\r', 't': '\t', '"': '"', "'": "'", '\\': '\\', '0': '\0'}
    while i < len(s):
        c = s[i]
        if c == '\\' and i + 1 < len(s):
            n = s[i + 1]
            if n == 'u':
                out.append(chr(int(s[i + 2:i + 6], 16))); i += 6; continue
            out.append(m.get(n, n)); i += 2; continue
        out.append(c); i += 1
    return ''.join(out)

def unescape_xaml(s):
    for k, v in XAML_ENT.items():
        s = s.replace(k, v)
    return re.sub(r'\\(.)', r'\1', s)

def main():
    keys = {}
    for path in sorted((ROOT / 'src').rglob('*')):
        if any(p in path.parts for p in ('bin', 'obj')):
            continue
        if path.suffix == '.cs':
            text = path.read_text(encoding='utf-8')
            for m in CS_CALL.finditer(text):
                keys.setdefault(unescape_cs(m.group(1)), path.name)
        elif path.suffix == '.xaml':
            text = path.read_text(encoding='utf-8')
            for m in XAML.finditer(text):
                keys.setdefault(unescape_xaml(m.group(1)), path.name)
    for path in (ROOT / 'src').rglob('*.json'):
        if 'bin' in path.parts or 'obj' in path.parts or 'Locales' in path.parts:
            continue
        if 'Preset' in path.name or 'preset' in path.name:
            def walk(o):
                if isinstance(o, dict):
                    for k, v in o.items():
                        if k in ('displayName', 'description') and isinstance(v, str):
                            keys.setdefault(v, path.name)
                        else:
                            walk(v)
                elif isinstance(o, list):
                    for v in o: walk(v)
            try: walk(json.loads(path.read_text(encoding='utf-8')))
            except json.JSONDecodeError: pass
    cjk = re.compile(r'[\u3000-\u9fff\uff00-\uffef]')
    result = [k for k in keys if cjk.search(k)]
    out = json.dumps(result, ensure_ascii=False, indent=1)
    if len(sys.argv) > 2 and sys.argv[1] == '--out':
        pathlib.Path(sys.argv[2]).write_text(out, encoding='utf-8')
    else:
        print(out)

if __name__ == '__main__':
    main()
