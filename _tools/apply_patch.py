# -*- coding: utf-8 -*-
"""
Applies a patch file to PlayerController.cs.

PlayerController.cs is GBK, no BOM, CRLF. The Edit/Write tools write UTF-8 and
would destroy the Chinese comments, so every change to this file goes through
here. Also note the Write tool does not add a BOM - which is what we want here,
since this file must stay BOM-less.

Patch file format (see _patch_pc3.txt):
    ###R1S###      <- old text starts
    ...old...
    ###R1M###      <- new text starts
    ...new...
    ###R1E###      <- block ends

Block line endings are LF in the file and converted to CRLF here. Every old
block must match exactly once; anything else aborts before writing.
"""
import re
import sys

PC = r'E:\unity\求职demo\Assets\Script\PlayerController.cs'
PATCH = sys.argv[1] if len(sys.argv) > 1 else r'E:\unity\求职demo\_patch_pc3.txt'

with open(PC, 'rb') as f:
    raw = f.read()

if raw[:3] == b'\xef\xbb\xbf':
    print('ERROR: file has a UTF-8 BOM, aborting')
    sys.exit(1)

src = raw.decode('gbk')

with open(PATCH, 'r', encoding='utf-8') as f:
    pf = f.read()

blocks = []
pat = re.compile(r'###R(\d+)S###\n(.*?)###R\1M###\n(.*?)###R\1E###\n', re.S)
for m in pat.finditer(pf):
    blocks.append((m.group(1), m.group(2), m.group(3)))

print('patch file: %s' % PATCH)
print('parsed blocks:', len(blocks))

if not blocks:
    print('ERROR: no blocks parsed')
    sys.exit(1)


def to_crlf(s):
    return s.replace('\r\n', '\n').replace('\n', '\r\n')


failed = []

for (num, old, new) in blocks:
    old_c = to_crlf(old)
    new_c = to_crlf(new)
    n = src.count(old_c)

    if n != 1:
        failed.append('R%s count=%d' % (num, n))
        continue

    src = src.replace(old_c, new_c, 1)
    print('  R%-3s ok' % num)

if failed:
    print('FAILED (nothing written):', failed)
    sys.exit(1)

out = src.encode('gbk')

if out.decode('gbk') != src:
    print('ERROR: gbk round-trip mismatch')
    sys.exit(1)

if out[:3] == b'\xef\xbb\xbf':
    print('ERROR: result would have a BOM')
    sys.exit(1)

print('braces { %d  } %d' % (src.count('{'), src.count('}')))

if src.count('{') != src.count('}'):
    print('ERROR: unbalanced braces')
    sys.exit(1)

with open(PC, 'wb') as f:
    f.write(out)

print('written bytes: %d (was %d)' % (len(out), len(raw)))
print('CRLF %d  bare-LF %d' % (
    out.count(b'\r\n'), out.count(b'\n') - out.count(b'\r\n')))
