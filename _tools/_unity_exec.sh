#!/usr/bin/env bash
# 在 Unity batchmode 里跑一个编辑器静态方法（构建场景 / 接线用）。
#
# 和 _unity_test.sh 同一套绕坑办法：stdout 必须重定向，且 Unity 干完活
# 进程自己不退，所以后台起、轮询日志里的完成标记、拿到就 kill。
#
# 用法：_unity_exec.sh <类名.方法名> <等待的日志标记>
set -u

PROJ="E:/unity/求职demo"
SCRATCH="E:/unity/_rpgbuild"
UNITY="/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe"

METHOD="${1:?用法: _unity_exec.sh 类名.方法名 日志标记}"
MARKER="${2:-}"

LOG="$SCRATCH/exec_$(echo "$METHOD" | tr '.' '_').log"

mkdir -p "$SCRATCH"
rm -f "$LOG"

if [ -f "$PROJ/Temp/UnityLockfile" ]; then
    echo "项目被编辑器占着（Temp/UnityLockfile 存在），先关掉 Unity。"
    exit 1
fi

echo "=== Unity batchmode: $METHOD ==="
"$UNITY" -batchmode -nographics \
    -projectPath "$PROJ" \
    -executeMethod "$METHOD" \
    -logFile "$LOG" </dev/null >"$SCRATCH/exec_stdout.log" 2>&1 &

UNITY_PID=$!

# 脚本编译 + 打开场景 + 存盘，给足时间
for _ in $(seq 1 900); do
    # -F 必须加：标记写成 "[WireEquipment]" 的话，grep 会当正则的字符类，
    # 匹配到日志里任意一个 W 就算命中，于是秒杀 Unity（踩过）
    if [ -n "$MARKER" ] && [ -f "$LOG" ] && grep -qF "$MARKER" "$LOG" 2>/dev/null; then
        sleep 3
        break
    fi
    if ! kill -0 "$UNITY_PID" 2>/dev/null; then
        break
    fi
    sleep 1
done

if kill -0 "$UNITY_PID" 2>/dev/null; then
    kill -9 "$UNITY_PID" 2>/dev/null
    sleep 2
fi

rm -f "$PROJ/Temp/UnityLockfile" 2>/dev/null

echo "--- 日志里的标记行 ---"
grep -E "\[(BuildInventoryUI|WireEquipment)\]|error CS|Exception|Compilation failed" "$LOG" || true
