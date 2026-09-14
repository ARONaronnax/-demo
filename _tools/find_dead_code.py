# -*- coding: utf-8 -*-
"""
粗筛 Assets/Script 里的死代码：没被引用过的 private 字段、没被调用过的 private 方法。

这是启发式的，不是编译器级别。它只用来缩小人工复核的范围——
每一条都要看过再删。特别注意：

  * public 方法可能被 Unity 的 Animation Event / Inspector 调用，
    本脚本一律不碰，也请人工不要照着删。
  * 只出现 1 次 = 只有声明本身，没有任何使用点。
  * 只出现 2 次且其中一次是赋值、没有读取，也会被列出来，需要人工判断。

Usage: python _tools/find_dead_code.py
"""
import io
import glob
import re
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

ROOT = 'Assets/Script'


def strip_comments(s):
    """注释里的名字不算引用，否则注释掉的使用点会掩盖死代码。"""
    s = re.sub(r'/\*.*?\*/', ' ', s, flags=re.S)
    s = re.sub(r'//[^\n]*', ' ', s)
    return s


FIELD = re.compile(
    r'^[ \t]*(?:\[[^\]]*\][ \t]*)*(private)[ \t]+'
    r'(?:static[ \t]+)?(?:readonly[ \t]+)?'
    r'[\w<>\[\],\.\? ]+?[ \t]+(_?[A-Za-z]\w*)[ \t]*(?:=|;)', re.M)

METHOD = re.compile(
    r'^[ \t]*(?:\[[^\]]*\][ \t]*)*(private)[ \t]+'
    r'(?:static[ \t]+)?(?:virtual[ \t]+)?(?:override[ \t]+)?'
    r'[\w<>\[\],\.\? ]+[ \t]+([A-Za-z]\w*)[ \t]*\(', re.M)

UNITY_MESSAGES = {
    'Awake', 'Start', 'Update', 'LateUpdate', 'FixedUpdate',
    'OnEnable', 'OnDisable', 'OnDestroy', 'Reset', 'OnValidate',
    'OnTriggerEnter', 'OnTriggerExit', 'OnCollisionEnter', 'OnCollisionExit',
    'OnDrawGizmos', 'OnGUI', 'OnApplicationQuit',
}

files = {}

for path in glob.glob('%s/**/*.cs' % ROOT, recursive=True):
    files[path.replace('\\', '/')] = io.open(
        path, encoding='utf-8-sig', errors='replace').read()

print('扫描 %d 个文件' % len(files))
print()
print('=== private 字段：没有任何读取点 ===')

for path in sorted(files):
    clean = strip_comments(files[path])

    for m in FIELD.finditer(files[path]):
        name = m.group(2)

        uses = len(re.findall(r'\b%s\b' % re.escape(name), clean))

        if uses <= 1:
            print('  %-46s %s' % (path, name))

print()
print('=== private 方法：没有任何调用点 ===')

for path in sorted(files):
    clean = strip_comments(files[path])

    for m in METHOD.finditer(files[path]):
        name = m.group(2)

        if name in UNITY_MESSAGES:
            continue

        uses = len(re.findall(r'\b%s[ \t]*\(' % re.escape(name), clean))

        if uses <= 1:
            print('  %-46s %s()' % (path, name))

print()
print('=== 调试 / 临时代码线索 ===')

PATTERNS = ['Debug.Log', 'verboseLog', 'KeyCode.', '#if UNITY_EDITOR',
            'TODO', 'FIXME', 'HACK', '临时', '调试', '测试用']

for path in sorted(files):
    hits = []

    for i, line in enumerate(files[path].split('\n'), 1):
        for pat in PATTERNS:
            if pat in line:
                hits.append((i, pat, line.strip()[:88]))
                break

    if hits:
        print('  %s' % path)

        for i, pat, text in hits[:14]:
            print('    %5d [%s] %s' % (i, pat, text))

        if len(hits) > 14:
            print('    ... 还有 %d 处' % (len(hits) - 14))
