#!/usr/bin/env python
"""把新建的 .cs 文件加进 Unity 生成的 .csproj 的 Compile 列表里。

为什么需要它：Unity 的 .csproj 是**显式枚举**源文件的，新加的 .cs 不在列表里，
`_build_and_test.sh` 就完全看不到它（表现为 "type not found"，而不是编译错误）。
编辑器重新获得焦点时 Unity 会重写整个 .csproj，所以这里的改动是临时的、会被覆盖。

用法：
    python _tools/csproj_file.py Demo.Runtime.csproj Assets/Script/Inventory/InventorySystem.cs
"""
import sys


def to_entry(rel_path):
    return '    <Compile Include="%s" />' % rel_path.replace('/', '\\')


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 1

    csproj, rel_path = sys.argv[1], sys.argv[2]
    entry = to_entry(rel_path)

    with open(csproj, 'rb') as f:
        lines = f.read().split(b'\n')

    encoded = entry.encode('utf-8')

    for line in lines:
        if line.strip() == encoded.strip():
            print('already present: %s' % rel_path)
            return 0

    # 插到最后一条 Compile 之后，保持 Unity 自己的分组顺序不被打乱。
    last = -1
    for i, line in enumerate(lines):
        if line.strip().startswith(b'<Compile Include='):
            last = i

    if last < 0:
        print('no <Compile Include> block found in %s' % csproj)
        return 1

    lines.insert(last + 1, encoded)

    with open(csproj, 'wb') as f:
        f.write(b'\n'.join(lines))

    print('added to %s: %s' % (csproj, rel_path))
    return 0


if __name__ == '__main__':
    sys.exit(main())
