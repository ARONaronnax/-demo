# _tools

不参与打包的辅助脚本。放在项目根目录下是因为 Unity 只扫描 `Assets/`，这里不会被导入。

## `_build_and_test.sh`

不打开 Unity 就能编译 + 跑 EditMode 测试。

```bash
bash _tools/_build_and_test.sh
```

**为什么需要它：** Unity 的 batchmode 打不开已经被编辑器占用的项目（退出码 21），
所以"跑测试前先关 Unity"原本是唯一办法。这个脚本改用 Unity 自带的 Roslyn 编译器
（`MonoBleedingEdge/.../Roslyn/csc.exe`），读取 Unity 自己生成的 `.csproj`，
用**同样的源文件、同样的引用、同样的宏**编译一遍。

**能查出什么：** 编译错误（含中文注释被破坏、命名空间写错、事件签名改了但订阅方没改）。

**查不出什么：** 需要 UnityEngine 原生运行时的测试（`DestroyImmediate`、
`ScriptableObject.CreateInstance` 这类）在编辑器外跑不了，会报
`cant resolve internal call` 并被脚本标成 SKIP。这部分只能你在 Unity 里跑。

改动脚本前 Unity 会把你踢出去，所以这个脚本**在编辑器开着的时候也能用**。

## `_offline_compile.py`

上面那个脚本调用的编译器封装。也可以单独用：

```bash
DEMO_OUT_DIR=E:/unity/_rpgbuild python _tools/_offline_compile.py Demo.Runtime.csproj
```

`DEMO_OUT_DIR` 用来把输出挪出项目——默认输出目录在项目的 `Temp/` 下，
而那是 Unity 编辑器自己的暂存区，它随时会清空。

`E:/unity/_rpgbuild/Runner.cs` 是配套的最小反射测试执行器
（Unity 只带 NUnit 框架，不带命令行 runner）。它支持 `[SetUp]` / `[TearDown]`，
不支持 `[TestCase]`。它存在的意义是让纯逻辑测试能离线跑，不替代 Unity 的 runner。

## `apply_patch.py`

往 `Assets/Script/PlayerController.cs` 打补丁。

```bash
python _tools/apply_patch.py 补丁文件.txt
```

**必须走这个脚本的原因：** `PlayerController.cs` 是 **GBK、无 BOM、CRLF**。
Edit / Write 工具会按 UTF-8 写回，把中文注释全部变成乱码。脚本显式做
GBK 解码 → 替换 → GBK 编码，并做往返校验。

补丁格式见脚本头部注释。每段旧文本必须**恰好匹配一次**，否则整体放弃、不写盘。
