# -*- coding: utf-8 -*-
"""
Dumps an AnimatorController's graph: states with their motion, plus every
transition with its source, destination and conditions.

Reading this from the .controller YAML is much faster than clicking around the
Animator window, and unlike the window it shows condition references to
parameters that no longer exist (the 'uses parameter X which does not exist'
console warning) as an explicit MISSING marker.

Usage: python _tools/dump_animator.py Assets/.../SwordAndShieldStance.controller
"""
import io
import re
import sys

# FileIDs are frequently negative, so the & captures an optional minus sign.
DOC = re.compile(r'^--- !u!(\d+) &(-?\d+)', re.M)


def parse(text):
    """Split Unity YAML into {fileID: (classId, body)}."""
    docs = {}
    marks = list(DOC.finditer(text))

    for i, m in enumerate(marks):
        end = marks[i + 1].start() if i + 1 < len(marks) else len(text)
        docs[m.group(2)] = (int(m.group(1)), text[m.start():end])

    return docs


def field(body, name):
    m = re.search(r'^\s*m_%s: (.+)$' % name, body, re.M)
    return m.group(1).strip() if m else None


def ref_field(body, name):
    """A field holding '{fileID: N}' -> 'N' as a string, or None."""
    v = field(body, name)
    m = re.match(r'\{fileID: (-?\d+)\}', v) if v else None
    return m.group(1) if m else None


def refs(body, name):
    """fileIDs of a list field such as m_Transitions / m_AnyStateTransitions."""
    m = re.search(r'^\s*m_%s:\r?\n((?:\s*-\s*\{fileID: -?\d+\}\r?\n)+)' % name, body, re.M)
    return re.findall(r'fileID: (-?\d+)', m.group(1)) if m else []


def conditions(body):
    out = []
    m = re.search(r'^\s*m_Conditions:\r?\n((?:.+\r?\n)+?)(?=\s*m_DstStateMachine:)', body, re.M)

    if not m:
        return out

    for c in re.finditer(r'm_ConditionMode: (\d+)\r?\n\s*m_ConditionEvent: (\S+)', m.group(1)):
        mode = {'1': 'If', '2': 'IfNot', '3': '>', '4': '<', '6': '==', '7': '!='}.get(
            c.group(1), c.group(1))
        out.append('%s %s' % (mode, c.group(2)))

    return out


def main(path):
    text = io.open(path, encoding='utf-8').read()
    docs = parse(text)

    controller = next(b for c, b in docs.values() if c == 91)

    start = controller.find('m_AnimatorParameters:')
    end = controller.find('m_AnimatorLayers:')
    params = {}

    if start >= 0 and end > start:
        for p in re.finditer(r'm_Name: (\S+)\r?\n\s*m_Type: (\d+)',
                             controller[start:end]):
            params[p.group(1)] = {'1': 'Float', '3': 'Int', '4': 'Bool', '9': 'Trigger'}.get(
                p.group(2), p.group(2))

    print('parameters (%d):' % len(params))
    for k, v in params.items():
        print('   %-34s %s' % (k, v))

    def kind_of(fid):
        return docs.get(fid, (None, ''))[0]

    # AnimatorState -> name + the clip it plays
    names = {}
    motions = {}
    for fid, (cid, body) in docs.items():
        if cid == 1102:
            names[fid] = field(body, 'Name')
            m = re.search(r'm_Motion: \{fileID: (-?\d+), guid: (\w+)', body)
            motions[fid] = m.group(2) if m else ''

    print()
    print('transitions:')
    missing_params = set()

    for fid, (cid, body) in docs.items():
        if cid != 1101:
            continue

        dst = ref_field(body, 'DstState')
        dst_name = names.get(dst, dst or '(exit)')

        conds = conditions(body)

        for c in conds:
            p = c.split()[-1]
            if p not in params:
                missing_params.add(p)

        # Which state or machine owns this transition
        owner = None
        for ofid, (ocid, obody) in docs.items():
            if fid in refs(obody, 'Transitions') or fid in refs(obody, 'AnyStateTransitions'):
                owner = (ocid, ofid)
                break

        owner_label = '?'

        if owner:
            ocid, ofid = owner
            if fid in refs(docs[ofid][1], 'AnyStateTransitions'):
                owner_label = 'AnyState'
            elif ocid == 1102:
                owner_label = names.get(ofid, ofid)
            elif ocid == 1107:
                owner_label = 'SM:' + (field(docs[ofid][1], 'Name') or '')

        exit_time = field(body, 'HasExitTime')
        dur = field(body, 'TransitionDuration')
        self_ok = field(body, 'CanTransitionToSelf')

        print('   %-34s -> %-34s  %s' % (
            owner_label, dst_name,
            ', '.join(conds) if conds else ('NO CONDITION (ignored)' if exit_time == '0' else '')))
        print('        exitTime=%s duration=%s canSelf=%s' % (exit_time, dur, self_ok))

    if missing_params:
        print()
        print('!! conditions reference parameters that DO NOT EXIST: %s'
              % ', '.join(sorted(missing_params)))


if __name__ == '__main__':
    main(sys.argv[1])
