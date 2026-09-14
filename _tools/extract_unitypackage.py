#!/usr/bin/env python
"""把 .unitypackage 解开直接铺进 Assets/。

为什么需要它：`AssetDatabase.ImportPackage` 是**异步**的，batchmode + `-quit`
会在导入完成前就退出进程，包内容一个都不会落地。这个脚本同步解包，行为确定。

.unitypackage 就是个 gzip 过的 tar，每个资源一个以 GUID 命名的目录，里面：
    pathname    目标路径（第一行）
    asset       资源本体
    asset.meta  资源 meta（不是每个包都有）

用法：
    python _tools/extract_unitypackage.py <包文件> <目标目录>
"""
import os
import sys
import tarfile


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 1

    package, dest = sys.argv[1], sys.argv[2]

    if not os.path.isfile(package):
        print('找不到包: %s' % package)
        return 1

    written = 0
    skipped = 0

    with tarfile.open(package, 'r:gz') as tar:
        names = tar.getnames()

        for name in names:
            if not name.endswith('/pathname'):
                continue

            folder = name[:-len('/pathname')]
            target = tar.extractfile(name).read().decode('utf-8').splitlines()[0].strip()

            if not target:
                continue

            target_path = os.path.join(dest, target)

            def read(member):
                full = folder + '/' + member
                if full not in names:
                    return None
                return tar.extractfile(full).read()

            body = read('asset')

            if body is None:
                # 没有 asset 的条目是目录占位
                os.makedirs(target_path, exist_ok=True)
                skipped += 1
                continue

            os.makedirs(os.path.dirname(target_path), exist_ok=True)

            with open(target_path, 'wb') as f:
                f.write(body)

            meta = read('asset.meta')

            if meta is not None:
                with open(target_path + '.meta', 'wb') as f:
                    f.write(meta)

            written += 1

    print('写入 %d 个资源，%d 个目录占位 -> %s' % (written, skipped, dest))
    return 0


if __name__ == '__main__':
    sys.exit(main())
