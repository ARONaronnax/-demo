## Task 3: AnimationClipLocator 剪辑定位

把"从哪个 fbx 里取哪个剪辑"这件事收进一个类。资源包的布局**三套并存**，必须逐一区分：移动类在 `InPlace/` 与 `RootMotion/` 各一份（名字中缀不同，分别是 `_InPlace_` 与 `_RM_`）；动作类（待机/防御/受击/死亡等）只有一份且放在**架势根目录**；连招类两份分散在前两个目录里。路径必须显式构造，不能靠猜。

**Files:**
- Create: `Assets/Script/Editor/AnimationClipLocator.cs`
- Test: `Assets/Tests/EditMode/AnimationClipLocatorTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:
  - `Demo.EditorTools.AnimationClipLocator.InPlacePath(string clipName) -> string`
  - `Demo.EditorTools.AnimationClipLocator.RootMotionPath(string clipName) -> string`
  - `Demo.EditorTools.AnimationClipLocator.LoadClip(string fbxPath) -> AnimationClip`
  - `Demo.EditorTools.AnimationClipLocator.LoadInPlace(string) -> AnimationClip`
    （先查 `InPlace/`，取不到回退到架势根目录 —— 动作类剪辑只存在于根目录）
  - `Demo.EditorTools.AnimationClipLocator.StanceRootPath(string) -> string`
  - `Demo.EditorTools.AnimationClipLocator.LoadRootMotion(string) -> AnimationClip`

- [ ] **Step 1: 写测试**

`Assets/Tests/EditMode/AnimationClipLocatorTests.cs`：

```csharp
using NUnit.Framework;
using UnityEngine;
using Demo.EditorTools;

namespace Demo.Tests
{
    public class AnimationClipLocatorTests
    {
        [Test]
        public void InPlace路径_指向InPlace目录()
        {
            var p = AnimationClipLocator.InPlacePath("MoveFWD_Battle_InPlace_SwordAndShield");
            StringAssert.Contains("/InPlace/", p);
            StringAssert.EndsWith(".fbx", p);
        }

        [Test]
        public void RootMotion路径_指向RootMotion目录()
        {
            var p = AnimationClipLocator.RootMotionPath("RollFWD_Battle_RM_SwordAndShield");
            StringAssert.Contains("/RootMotion/", p);
        }

        [Test]
        public void 加载InPlace剪辑_返回非空且名称匹配()
        {
            var clip = AnimationClipLocator.LoadInPlace("MoveFWD_Battle_InPlace_SwordAndShield");
            Assert.IsNotNull(clip, "找不到剪辑，检查 fbx 路径或剪辑命名");
            StringAssert.Contains("MoveFWD_Battle_InPlace_SwordAndShield", clip.name);
        }

        [Test]
        public void 加载RootMotion剪辑_返回非空()
        {
            var clip = AnimationClipLocator.LoadRootMotion("RollFWD_Battle_RM_SwordAndShield");
            Assert.IsNotNull(clip);
        }

        [Test]
        public void 加载不存在的剪辑_返回null而非抛异常()
        {
            var clip = AnimationClipLocator.LoadInPlace("完全不存在的剪辑名");
            Assert.IsNull(clip);
        }

        [Test]
        public void 同名的InPlace与RootMotion剪辑_是两个不同对象()
        {
            var a = AnimationClipLocator.LoadInPlace("MoveFWD_Battle_InPlace_SwordAndShield");
            var b = AnimationClipLocator.LoadRootMotion("MoveFWD_Battle_RM_SwordAndShield");
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreNotSame(a, b);
        }
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 编译失败，`AnimationClipLocator` 不存在。

- [ ] **Step 3: 实现 AnimationClipLocator**

`Assets/Script/Editor/AnimationClipLocator.cs`：

```csharp
using UnityEditor;
using UnityEngine;

namespace Demo.EditorTools
{
    /// <summary>
    /// 从资源的 fbx 文件中定位并加载 AnimationClip。
    ///
    /// 资源包的布局三套并存，已逐一核对过磁盘：
    ///   - 位移类：InPlace/ 与 RootMotion/ 各一份，名字中缀分别是 `_InPlace_` 与 `_RM_`，
    ///     Animation/SwordAndShield/InPlace/&lt;名字&gt;.fbx
    ///     Animation/SwordAndShield/RootMotion/&lt;名字&gt;.fbx
    ///   - 动作类（待机/防御/受击/死亡等）：只有一份，且放在**架势根目录**，
    ///     Animation/SwordAndShield/&lt;名字&gt;.fbx
    ///   - 连招类：两份分散在上面两个目录里
    /// 因此必须显式构造路径，不能只靠名字查找。
    /// </summary>
    public static class AnimationClipLocator
    {
        public const string HeroAnimationRoot = "Assets/RPGTinyHeroWavePBR/Animation/SwordAndShield";
        public const string InPlaceDir = HeroAnimationRoot + "/InPlace";
        public const string RootMotionDir = HeroAnimationRoot + "/RootMotion";

        // 架势根目录：动作类剪辑唯一的一份就放在这里
        public static string StanceRootPath(string clipName) => $"{HeroAnimationRoot}/{clipName}.fbx";

        public static string InPlacePath(string clipName) => $"{InPlaceDir}/{clipName}.fbx";

        public static string RootMotionPath(string clipName) => $"{RootMotionDir}/{clipName}.fbx";

        /// <summary>
        /// 加载「不驱动根运动」的那一版剪辑。
        ///
        /// 资源包的布局并不统一（已逐一核对过磁盘）：
        ///   - 移动类（Move*/Sprint*/Jump*）：InPlace/ 与 RootMotion/ 各一份，名字中缀不同
        ///   - 动作类（Idle/Defend/DefendHit/Dizzy/GetHit*/GetUp/Die*）：**只有一份，放在架势根目录**，
        ///     InPlace/ 与 RootMotion/ 里都没有
        ///   - 连招类：Combo01_RM_* 在 RootMotion/，Combo05_InPlaceWithRMHeight_* 在 InPlace/
        /// 所以先查 InPlace/，取不到再回退到架势根目录。
        /// 「不驱动根运动」对动作类剪辑而言本来就是根目录那一份，语义一致，不是权宜之计。
        /// 当前 19 个被引用的名字里没有任何一个同时存在于这两处，回退不会造成歧义。
        /// </summary>
        public static AnimationClip LoadInPlace(string clipName) =>
            LoadClip(InPlacePath(clipName)) ?? LoadClip(StanceRootPath(clipName));

        public static AnimationClip LoadRootMotion(string clipName) => LoadClip(RootMotionPath(clipName));

        /// <summary>
        /// 加载 fbx 中的主 AnimationClip。
        /// fbx 里可能含多个子资源（网格、材质、多个 take），
        /// 这里取名字最匹配的那个，并跳过 Unity 生成的 __preview__ 剪辑。
        /// </summary>
        public static AnimationClip LoadClip(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets == null || assets.Length == 0)
                return null;

            string wanted = System.IO.Path.GetFileNameWithoutExtension(fbxPath);

            AnimationClip best = null;
            foreach (var asset in assets)
            {
                if (asset is not AnimationClip clip)
                    continue;
                if (clip.name.StartsWith("__preview__"))
                    continue;

                // 完全同名，直接采用
                if (clip.name == wanted)
                    return clip;

                // 否则记住第一个候选，作为兜底
                if (best == null)
                    best = clip;
            }

            return best;
        }
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 6 个测试全部 PASS。

若"加载InPlace剪辑"失败，说明 fbx 内剪辑命名与文件名不一致。用下面的调试代码确认实际名字，然后调整匹配逻辑：

```csharp
foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
    Debug.Log($"{a.GetType().Name} :: {a.name}");
```

- [ ] **Step 5: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Editor/AnimationClipLocator.cs Assets/Tests/EditMode/AnimationClipLocatorTests.cs
git commit -m "feat: 新增 AnimationClipLocator 按目录区分 InPlace 与 RootMotion 剪辑"
```

---

