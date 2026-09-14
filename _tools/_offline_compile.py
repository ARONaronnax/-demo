# -*- coding: utf-8 -*-
"""
Offline compile check using Unity's bundled Roslyn csc.
Reads an Unity-generated .csproj and re-runs its own compiler invocation,
so this is the same compilation Unity would do (same refs, same defines).

Usage: python _offline_compile.py Demo.Runtime.csproj
"""
import os
import re
import sys
import subprocess
import xml.etree.ElementTree as ET

UNITY = r'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Data'
CSC_DLL = os.path.join(UNITY, 'DotNetSdkRoslyn', 'csc.dll')
NETCORERUN = os.path.join(UNITY, 'Tools', 'netcorerun', 'netcorerun.exe')
MANAGED = os.path.join(UNITY, 'Managed', 'UnityEngine')
NETSTANDARD = os.path.join(UNITY, 'NetStandard', 'ref', '2.1.0')

NS = '{http://schemas.microsoft.com/developer/msbuild/2003}'

# Set DEMO_OUT_DIR to keep build output outside the project (see _build_and_test.sh).
OUT_OVERRIDE = os.environ.get('DEMO_OUT_DIR') or None


def tag(el, name):
    return el.find(NS + name)


def quoted(path):
    """csc response files split on whitespace, so quote anything with a space."""
    return '"' + path + '"' if ' ' in path else path


def main(csproj):
    root = os.path.dirname(os.path.abspath(csproj))
    tree = ET.parse(csproj)
    proj = tree.getroot()

    sources = []
    for c in proj.iter(NS + 'Compile'):
        inc = c.get('Include')
        if inc:
            sources.append(os.path.join(root, inc.replace('\\', os.sep)))

    refs = []
    for r in proj.iter(NS + 'Reference'):
        hp = tag(r, 'HintPath')
        if hp is not None and hp.text:
            refs.append(hp.text)
        else:
            name = r.get('Include')
            cand = os.path.join(MANAGED, name + '.dll')
            if os.path.isfile(cand):
                refs.append(cand)

    # asmdef dependencies come through as ProjectReference; point each at the
    # same output dir this script builds into, so an asmdef dependency resolves
    # to freshly compiled code rather than a stale DLL from an earlier run.
    for pr in proj.iter(NS + 'ProjectReference'):
        sub = os.path.join(root, pr.get('Include').replace('\\', os.sep))
        if not os.path.isfile(sub):
            print('   MISSING PROJECT %s' % sub)
            continue
        subroot = ET.parse(sub).getroot()
        asm = None
        for pg in subroot.iter(NS + 'PropertyGroup'):
            a = tag(pg, 'AssemblyName')
            if a is not None and a.text:
                asm = a.text
        if asm:
            refs.append(os.path.join(OUT_OVERRIDE or root, asm + '.dll'))

    defines = ''
    for pg in proj.iter(NS + 'PropertyGroup'):
        dc = tag(pg, 'DefineConstants')
        if dc is not None and dc.text and 'UNITY_2022' in dc.text:
            defines = dc.text.strip()
            break

    missing = [s for s in sources if not os.path.isfile(s)]
    refs = [r for r in refs if os.path.isfile(r)]

    print('sources   : %d (missing %d)' % (len(sources), len(missing)))
    for m in missing:
        print('   MISSING %s' % m)
    print('references: %d' % len(refs))
    print('defines   : %d symbols' % (len(defines.split(';')) if defines else 0))

    # Build into DEMO_OUT_DIR when set, otherwise the csproj's own OutputPath.
    # The csproj default is under the project's Temp/, which the running Unity
    # editor treats as its own scratch space and deletes out from under us.
    asm_name = 'Demo.Runtime'
    for pg in proj.iter(NS + 'PropertyGroup'):
        a = tag(pg, 'AssemblyName')
        if a is not None and a.text:
            asm_name = a.text

    out_dir = OUT_OVERRIDE or os.path.join(root, 'Temp', 'bin', 'Debug')
    os.makedirs(out_dir, exist_ok=True)

    out = os.path.join(out_dir, asm_name + '.dll')
    print('output    : %s' % out)

    # Windows caps a process command line at ~32k chars and 221 references
    # blow past it, so everything goes into a csc response file instead.
    rsp = os.path.join(root, 'Temp', '_offline_check.rsp')
    os.makedirs(os.path.dirname(rsp), exist_ok=True)

    lines = ['-nologo', '-nostdlib+', '-target:library',
             '-langversion:9.0', '-warn:0', '-nowarn:0169',
             '-out:' + quoted(out),
             '-define:' + defines]
    lines += ['-reference:' + quoted(r) for r in refs]
    lines += [quoted(s) for s in sources]

    with open(rsp, 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines))

    args = [NETCORERUN, CSC_DLL, '@' + rsp]

    print('compiling (rsp %d bytes) ...' % os.path.getsize(rsp))
    p = subprocess.run(args, capture_output=True, text=True, errors='replace')
    out_txt = (p.stdout or '') + (p.stderr or '')

    errs = [l for l in out_txt.splitlines() if ': error ' in l]
    print('exit code : %d' % p.returncode)
    print('errors    : %d' % len(errs))
    for e in errs[:60]:
        print('   ' + e)

    if not errs and not out_txt.strip():
        print('no diagnostics at all')

    return 1 if errs or p.returncode != 0 else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else 'Demo.Runtime.csproj'))
