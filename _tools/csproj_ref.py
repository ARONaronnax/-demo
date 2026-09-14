#!/usr/bin/env python
"""给 Unity 生成的 .csproj 补一条 <Reference>。

为什么需要它：和 csproj_file.py 同理 —— .csproj 是显式枚举的。
新引入一个包程序集（比如 Unity.TextMeshPro）之后，Unity 还没重新生成
.csproj 之前，离线编译看不到它。Unity 下次获得焦点时会重写整个文件，
所以这里的改动是临时的。

用法：
    python _tools/csproj_ref.py Demo.Runtime.csproj Unity.TextMeshPro
"""
import re
import sys


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 1

    csproj, name = sys.argv[1], sys.argv[2]

    with open(csproj, 'r', encoding='utf-8-sig', newline='') as f:
        text = f.read()

    if '<Reference Include="%s">' % name in text:
        print('已存在，跳过: %s -> %s' % (csproj, name))
        return 0

    nl = '\r\n' if '\r\n' in text else '\n'

    hint = r'Library\ScriptAssemblies\%s.dll' % name
    block = nl.join([
        '    <Reference Include="%s">' % name,
        '      <HintPath>%s</HintPath>' % hint,
        '      <Private>False</Private>',
        '    </Reference>',
        '',
    ])

    # 插在最后一个 </Reference> 之后。csproj 是 CRLF，别只认 \n。
    last = None
    for m in re.finditer(r'^    </Reference>\r?\n', text, re.M):
        last = m
    if last is None:
        print('找不到 </Reference>，%s 结构意外' % csproj)
        return 1

    text = text[:last.end()] + block + text[last.end():]

    with open(csproj, 'w', encoding='utf-8-sig', newline='') as f:
        f.write(text)

    print('added to %s: %s' % (csproj, name))
    return 0


if __name__ == '__main__':
    sys.exit(main())
