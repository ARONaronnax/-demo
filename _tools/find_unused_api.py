# -*- coding: utf-8 -*-
"""
列出 public 成员中，在本文件之外一次都没被引用过的。

**这份清单不能照单全删。** public 成员有三类看不见的调用方：

  1. Unity 的 Animation Event（按名字反射调用）——
     AttackStart / AttackEnd / GetHit / HitEnd / JumpStart / JumpEnd /
     GetUpEnd / Knockback 之类，删掉动画立刻失效。
  2. Inspector 里手拖的 UnityEvent 目标。
  3. 你还没接上的功能（比如等正式 UI 来订阅的事件）。

所以脚本只负责把范围缩小，每一条都要人工确认。
已知动画事件名在 ANIM_EVENT_SAFE 里，会被单独标出来。

Usage: python _tools/find_unused_api.py
"""
import io
import glob
import re
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

ROOT = 'Assets/Script'

# 这些名字挂在 Animation Event 上，绝对不能删
ANIM_EVENT_SAFE = {
    'AttackStart', 'AttackEnd', 'HitEnd', 'JumpStart', 'JumpEnd',
    'GetUpEnd', 'Knockback', 'OnAttackHit', 'AttackHit', 'FootStep',
}

MEMBER = re.compile(
    r'^[ \t]*(?:\[[^\]]*\][ \t]*)*public[ \t]+'
    r'(?:static[ \t]+)?(?:readonly[ \t]+)?(?:virtual[ \t]+)?'
    r'(?:event[ \t]+)?'
    r'[\w<>\[\],\.\? ]+?[ \t]+([A-Za-z]\w*)[ \t]*(?:[({=;])', re.M)


def strip_comments(s):
    s = re.sub(r'/\*.*?\*/', ' ', s, flags=re.S)
    s = re.sub(r'//[^\n]*', ' ', s)
    return s


files = {}

for path in glob.glob('%s/**/*.cs' % ROOT, recursive=True):
    files[path.replace('\\', '/')] = io.open(
        path, encoding='utf-8-sig', errors='replace').read()

print('=== public 成员：本文件之外零引用 ===')
print()

for path in sorted(files):
    decl = files[path]
    clean_self = strip_comments(decl)

    others = '\n'.join(
        strip_comments(v) for k, v in files.items() if k != path)

    hits = []

    for m in MEMBER.finditer(decl):
        name = m.group(1)

        if name in ('get', 'set', 'class', 'struct', 'enum', 'interface'):
            continue

        # 同文件内出现次数（排除声明本身）
        if len(re.findall(r'\b%s\b' % re.escape(name), clean_self)) > 1:
            continue

        if re.search(r'\b%s\b' % re.escape(name), others):
            continue

        hits.append(name)

    if hits:
        print('  %s' % path)

        for name in hits:
            tag = '  [Animation Event 候选，勿删]' if name in ANIM_EVENT_SAFE else ''
            print('      %s%s' % (name, tag))

        print()
