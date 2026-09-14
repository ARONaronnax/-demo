# -*- coding: utf-8 -*-
"""
第二轮清理扫描：多余的 using、注释掉的大段代码。

Usage: python _tools/find_dead_code2.py
"""
import io
import glob
import os
import re
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

BACKSLASH = chr(92)

# 命名空间 -> 该空间下本项目可能用到的类型名。
# 只用来判"这个 using 到底有没有被用到"，所以不要求穷举，
# 漏掉某个类型只会让 using 被误判成多余，人工复核时能看出来。
KNOWN = {
    'UnityEngine': [
        'GameObject', 'Transform', 'Vector3', 'Vector2', 'Quaternion',
        'Mathf', 'Debug', 'Input', 'Time', 'Animator', 'Physics', 'Collider',
        'LayerMask', 'RaycastHit', 'CharacterController', 'MonoBehaviour',
        'SerializeField', 'Tooltip', 'Header', 'RequireComponent',
        'ScriptableObject', 'CreateAssetMenu', 'Random', 'KeyCode', 'Color',
        'GUIStyle', 'GUI', 'Rect', 'Screen', 'RuntimeInitializeOnLoadMethod',
        'QueryTriggerInteraction', 'Application', 'Cursor', 'Texture2D',
    ],
    'System': ['Action', 'Func', 'String', 'Exception', 'Array', 'Serializable'],
    'System.Collections': ['IEnumerator'],
    'System.Collections.Generic': [
        'List', 'Dictionary', 'HashSet', 'IEnumerable', 'Queue', 'Stack'],
    'Demo.Combat': ['DamageCalculator', 'HealthComponent', 'Hitbox', 'ComboChain'],
    'Demo.Core': [
        'EventBus', 'InputLock', 'DamageInfo', 'IDamageable', 'InteractedEvent',
        'PlayerDiedEvent', 'EnemyDiedEvent', 'InteractionPromptChangedEvent',
        'QuestAcceptedEvent', 'QuestProgressChangedEvent', 'QuestCompletedEvent',
        'DialogueStartedEvent', 'DialogueLineChangedEvent', 'DialogueEndedEvent',
        'EntityDamagedEvent', 'ItemPickedUpEvent'],
    'Demo.Data': [
        'WeaponData', 'ItemData', 'QuestData', 'DialogueData', 'ConsumableData',
        'QuestItemData'],
    'Demo.Player': ['PlayerStats'],
    'Demo.Enemy': ['EnemyBrain', 'EnemyState', 'EnemySensors', 'EnemyIntents'],
    'Demo.Quest': ['QuestSystem', 'QuestComponent', 'QuestStatus'],
    'Demo.Interaction': ['IInteractable', 'NpcInteractable', 'DroppedItem'],
    'Demo.Dialogue': ['DialogueSystem', 'DialogueRunner'],
}


def rel(path):
    return path.replace(BACKSLASH, '/')


print('=== 疑似多余的 using ===')

for path in sorted(glob.glob('Assets/Script/**/*.cs', recursive=True)):
    src = io.open(path, encoding='utf-8-sig', errors='replace').read()

    body = re.sub(r'^[ \t]*using [^\n]*\n', '', src, flags=re.M)
    body = re.sub(r'//[^\n]*', ' ', body)
    body = re.sub(r'/\*.*?\*/', ' ', body, flags=re.S)

    for m in re.finditer(r'^using ([A-Za-z_][\w\.]*);', src, re.M):
        ns = m.group(1)
        types = KNOWN.get(ns)

        if types is None:
            continue

        if not any(re.search(r'\b%s\b' % re.escape(t), body) for t in types):
            print('  %-46s using %s;' % (rel(path), ns))

print()
print('=== 疑似注释掉的大段代码（连续 3 行以上的 // 代码）===')

found = False

for path in sorted(glob.glob('Assets/Script/**/*.cs', recursive=True)):
    lines = io.open(path, encoding='utf-8-sig', errors='replace').read().split('\n')
    run = []
    start = 0

    def flush():
        global found
        if len(run) >= 3:
            found = True
            print('  %-46s %d-%d（%d 行）'
                  % (rel(path), start, start + len(run) - 1, len(run)))
            print('        %s' % run[0][:84])

    for i, line in enumerate(lines, 1):
        s = line.strip()
        codey = (s.startswith('//')
                 and not s.startswith('///')
                 and len(s) > 12
                 and re.search(r'[;{}()]\s*$', s))

        if codey:
            if not run:
                start = i
            run.append(s)
        else:
            flush()
            run = []

    flush()

if not found:
    print('  （没有）')

print()
print(os.linesep.join([]) if False else '', end='')
