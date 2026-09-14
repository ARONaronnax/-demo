#!/usr/bin/env bash
# Offline compile + run this project's EditMode tests, without opening Unity.
#
# Why this exists: Unity batchmode refuses to open a project that an editor
# already has open (exit 21), so "close Unity first" was the only way to run
# tests. This uses Unity's own bundled Roslyn compiler with the project's
# generated .csproj files, so it compiles the exact same source set with the
# exact same references and defines Unity would use.
#
# Caveat: tests that touch UnityEngine native code (DestroyImmediate,
# ScriptableObject.CreateInstance) cannot run outside the editor and will
# report "cant resolve internal call". That is a limitation of this harness,
# not a test failure.
set -u
# 没有 pipefail 的话，`python ... | tail -4 || exit 1` 拿到的是 tail 的退出码，
# 测试程序集编译失败（比如 error CS0234）也不会中止脚本，
# 后面照样打印 "TOTAL: N passed, 0 failed"，看着全绿其实是假绿。踩过一次。
set -o pipefail

TOOLS="$(cd "$(dirname "$0")" && pwd)"
PROJ="E:/unity/求职demo"
SCRATCH="E:/unity/_rpgbuild"          # ASCII path: mono chokes on 求职
U="/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Data"
MONO="$U/MonoBleedingEdge/bin/mono.exe"
ROSLYN="$U/MonoBleedingEdge/lib/mono/msbuild/Current/bin/Roslyn/csc.exe"
NUNIT="$PROJ/Library/PackageCache/com.unity.ext.nunit@1.0.6/net35/unity-custom/nunit.framework.dll"

export MONO_EXTERNAL_ENCODINGS="utf8:utf8"

cd "$PROJ" || exit 1
mkdir -p "$SCRATCH"

echo "=== compile Demo.Runtime ==="
DEMO_OUT_DIR="$SCRATCH" python "$TOOLS/_offline_compile.py" Demo.Runtime.csproj 2>&1 | tail -4 || exit 1

echo "=== compile Demo.Tests.EditMode ==="
DEMO_OUT_DIR="$SCRATCH" python "$TOOLS/_offline_compile.py" Demo.Tests.EditMode.csproj 2>&1 | tail -4 || exit 1

echo "=== stage scratch dir ==="
cp "$U/Managed/UnityEngine/"*.dll "$SCRATCH/" 2>/dev/null
cp "$NUNIT" "$SCRATCH/"

"$MONO" "$ROSLYN" -nologo -target:exe -out:"$SCRATCH/Runner.exe" \
    -reference:"$NUNIT" "$SCRATCH/Runner.cs" || exit 1

echo "=== run tests ==="
TOTAL_PASS=0
TOTAL_FAIL=0
WOULD_NOT_RUN=""

for C in $(grep -rho "public class [A-Za-z0-9_]*Tests" Assets/Tests/EditMode/*.cs \
           | awk '{print $3}' | sort -u); do

    OUT=$("$MONO" "$SCRATCH/Runner.exe" "$SCRATCH/Demo.Tests.EditMode.dll" \
          "Demo.Tests.$C" "$SCRATCH" 2>&1)

    if echo "$OUT" | grep -q "cant resolve internal call"; then
        WOULD_NOT_RUN="$WOULD_NOT_RUN $C"
        printf "%-28s SKIP (needs Unity native runtime)\n" "$C"
        continue
    fi

    LINE=$(echo "$OUT" | grep -E ": [0-9]+ passed, [0-9]+ failed" | tail -1)
    printf "%-28s %s\n" "$C" "${LINE:-NO RESULT}"

    P=$(echo "$LINE" | sed -n 's/.*: \([0-9]*\) passed.*/\1/p')
    F=$(echo "$LINE" | sed -n 's/.*passed, \([0-9]*\) failed.*/\1/p')
    TOTAL_PASS=$((TOTAL_PASS + ${P:-0}))
    TOTAL_FAIL=$((TOTAL_FAIL + ${F:-0}))

    if [ "${F:-0}" != "0" ]; then
        echo "$OUT" | grep -A2 "FAIL" | sed 's/^/      /'
    fi
done

echo
echo "TOTAL: $TOTAL_PASS passed, $TOTAL_FAIL failed"
[ -n "$WOULD_NOT_RUN" ] && echo "SKIPPED (Unity-native):$WOULD_NOT_RUN"

[ "$TOTAL_FAIL" = "0" ]
