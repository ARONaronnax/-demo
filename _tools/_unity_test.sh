#!/usr/bin/env bash
# 用 Unity batchmode 跑 EditMode 测试。
#
# 为什么不能只用 _build_and_test.sh：那个离线跑法碰不了 Unity 原生运行时，
# 凡是 new GameObject / CreateInstance / DestroyImmediate 的测试全被 SKIP，
# 等于没验证。真正的验收必须走这里。
#
# 前提：编辑器必须先关掉。项目被 Unity 打开着的时候 batchmode 会以 exit 21 拒绝启动。
set -u

# 用法：_unity_test.sh [EditMode|PlayMode]，默认 EditMode。
# PlayMode 用来看 EditMode 覆盖不到的东西：Object.Destroy（只在 Play 模式有效）、
# 真实的 OnEnable / OnDisable 生命周期。
PLATFORM="${1:-EditMode}"

PROJ="E:/unity/求职demo"
SCRATCH="E:/unity/_rpgbuild"          # ASCII 路径，日志和结果都放这
UNITY="/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe"

RESULTS="$SCRATCH/results_$PLATFORM.xml"
LOG="$SCRATCH/unity_test_$PLATFORM.log"

mkdir -p "$SCRATCH"
rm -f "$RESULTS"

if [ -f "$PROJ/Temp/UnityLockfile" ]; then
    echo "项目被编辑器占着（Temp/UnityLockfile 存在），先关掉 Unity。"
    exit 1
fi

echo "=== Unity batchmode: $PLATFORM tests ==="
# 两个坑，都得绕：
#   1. stdout/stderr 必须重定向掉，否则调用方的 `| tail` 等不到 EOF；
#   2. Unity 跑完测试、写完结果之后**进程自己不退**（停在关闭阶段），
#      傻等 exit code 会一直挂着。所以后台起，轮询结果文件，拿到就收工。
"$UNITY" -batchmode -nographics \
    -projectPath "$PROJ" \
    -runTests -testPlatform "$PLATFORM" \
    -testResults "$RESULTS" \
    -logFile "$LOG" </dev/null >"$SCRATCH/unity_stdout.log" 2>&1 &

UNITY_PID=$!

for _ in $(seq 1 900); do
    if [ -f "$RESULTS" ]; then
        # 结果文件先落地、内容后写完，等它稳定下来
        sleep 5
        break
    fi
    sleep 1
done

if kill -0 "$UNITY_PID" 2>/dev/null; then
    kill -9 "$UNITY_PID" 2>/dev/null
    sleep 2
fi

# 顺手清掉可能残留的锁，不然下次启动会被自己挡住
rm -f "$PROJ/Temp/UnityLockfile" 2>/dev/null

if [ ! -f "$RESULTS" ]; then
    echo "没产出结果文件，看日志：$LOG"
    tail -40 "$LOG"
    exit 1
fi

python - "$RESULTS" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
total = failed = skipped = 0

for suite in root.iter('test-suite'):
    if suite.get('type') != 'TestFixture':
        continue
    r = suite.get('result')
    print('%-30s %s' % (suite.get('name'), r))

for case in root.iter('test-case'):
    total += 1
    if case.get('result') == 'Failed':
        failed += 1
        print('  FAIL %s' % case.get('fullname'))
        msg = case.find('failure/message')
        if msg is not None and msg.text:
            for line in msg.text.strip().splitlines():
                print('       %s' % line)
    elif case.get('result') in ('Skipped', 'Ignored'):
        skipped += 1

print()
print('TOTAL: %d passed, %d failed, %d skipped' % (total - failed - skipped, failed, skipped))
sys.exit(1 if failed else 0)
PY
