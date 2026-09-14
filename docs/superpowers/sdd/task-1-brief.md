## Task 1: 程序集骨架与测试装配

先建立三个程序集，并用一个冒烟测试证明测试链路是通的。**这一步必须最先做**——没有程序集定义，后续所有测试都无法引用被测代码。

**Files:**
- Create: `Assets/Script/Demo.Runtime.asmdef`
- Create: `Assets/Script/Editor/Demo.Editor.asmdef`
- Create: `Assets/Tests/EditMode/Demo.Tests.EditMode.asmdef`
- Create: `Assets/Tests/EditMode/AssemblySmokeTests.cs`

**Interfaces:**
- Consumes: 无
- Produces: 程序集 `Demo.Runtime` / `Demo.Editor` / `Demo.Tests.EditMode`

- [ ] **Step 1: 创建运行时程序集定义**

`Assets/Script/Demo.Runtime.asmdef`：

```json
{
    "name": "Demo.Runtime",
    "rootNamespace": "Demo",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: 创建编辑器程序集定义**

`Assets/Script/Editor/Demo.Editor.asmdef`：

```json
{
    "name": "Demo.Editor",
    "rootNamespace": "Demo.EditorTools",
    "references": [
        "Demo.Runtime"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: 创建测试程序集定义**

`Assets/Tests/EditMode/Demo.Tests.EditMode.asmdef`：

```json
{
    "name": "Demo.Tests.EditMode",
    "rootNamespace": "Demo.Tests",
    "references": [
        "Demo.Runtime",
        "Demo.Editor",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: 写冒烟测试**

`Assets/Tests/EditMode/AssemblySmokeTests.cs`：

```csharp
using NUnit.Framework;

namespace Demo.Tests
{
    public class AssemblySmokeTests
    {
        [Test]
        public void 测试程序集_已正确装配()
        {
            // 只断言 NotNull 等于没断言：程序集永远不为 null，asmdef 装配错了照样过。
            // 断言名字才能真正验证 asmdef 落在了预期的程序集里。
            Assert.AreEqual("Demo.Tests.EditMode",
                typeof(AssemblySmokeTests).Assembly.GetName().Name);
        }

        [Test]
        public void 编辑器程序集_可被测试程序集引用()
        {
            var type = typeof(Demo.EditorTools.NamespaceAnchor);
            Assert.IsNotNull(type);
            Assert.AreEqual("Demo.EditorTools", type.Namespace);
        }
    }
}
```

- [ ] **Step 5: 创建 `NamespaceAnchor`（让上一步的断言有意义）**

`Assets/Script/Editor/NamespaceAnchor.cs`：

```csharp
namespace Demo.EditorTools
{
    /// <summary>
    /// 仅用于验证 Demo.Editor 程序集可被测试程序集引用。
    /// 不代表任何业务含义，后续任务加入真正的编辑器工具后可保留。
    /// </summary>
    internal static class NamespaceAnchor
    {
    }
}
```

> 注意：`internal` 类型在测试程序集中不可见，除非加 `InternalsVisibleTo`。
> 因此这里必须是 `public`。改为：

```csharp
namespace Demo.EditorTools
{
    /// <summary>
    /// 仅用于验证 Demo.Editor 程序集可被测试程序集引用。
    /// </summary>
    public static class NamespaceAnchor
    {
    }
}
```

- [ ] **Step 6: 删除旧的 PlayerController.cs**

```bash
cd "E:/unity/求职demo"
rm Assets/Script/PlayerController.cs Assets/Script/PlayerController.cs.meta
```

旧文件在 `Assets/Script/` 根下，会与 `Assets/Script/Player/` 下的新文件重名冲突（同名类 `PlayerController`）。
Task 14 会在新位置重建它。

- [ ] **Step 7: 运行测试，确认通过**

图形界面方式：Unity 菜单 `Window > General > Test Runner` → `EditMode` 标签 → `Run All`。

命令行方式（**需要先关闭 Unity 编辑器**，否则会因项目被占用而失败）：

```bash
"/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics \
  -projectPath "E:/unity/求职demo" \
  -runTests -testPlatform EditMode \
  -testResults "E:/unity/求职demo/Temp/editmode-results.xml" \
  -logFile -
```

Expected: 两个测试通过，退出码 `0`。结果文件中 `<test-run result="Passed" passed="2" failed="0">`。

- [ ] **Step 8: 确认 Unity 未报编译错误**

打开 Unity，检查 Console 无红色错误。特别确认 `Assembly-CSharp` 中没有残留的 `PlayerController` 报错。

- [ ] **Step 9: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Demo.Runtime.asmdef Assets/Script/Editor/ \
        Assets/Tests/ Assets/Script/PlayerController.cs Assets/Script/PlayerController.cs.meta
git commit -m "build: 建立运行时/编辑器/测试三个程序集，移除旧 PlayerController"
```

---

