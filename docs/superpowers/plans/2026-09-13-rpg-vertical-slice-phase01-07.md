# RPG Demo Vertical Slice Phase 01–07 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有 Unity 项目上增量实现一条可完整游玩的纵向切片：篝火出生 → 找 NPC 对话 → 接"讨伐魔物"任务 → 战斗击杀 3 只怪 → 任务完成 → 怪物掉落武器 → 拾取。

**Architecture:** 纯逻辑 C# 类（`EventBus`/`DialogueSystem`/`QuestSystem`/`EnemyBrain`/`DamageCalculator`）承载全部可测行为，MonoBehaviour 只做 Inspector 接线与事件转发。系统间通过静态类型安全 `EventBus` 通信，`Enemy` 不引用 `QuestSystem`、不引用 `DropSpawner`。所有伤害判定用逐帧显式 `Physics.OverlapSphere`，不用 `OnTriggerEnter`，避免碰撞体/Layer/Rigidbody 配置静默失败。

**Tech Stack:** Unity 2022.3.62f3c1、Built-in 渲染管线、旧版 Input Manager、NUnit + Unity Test Framework 1.1.33（EditMode）、IMGUI 临时调试 HUD。

**Spec:** `docs/superpowers/specs/2026-09-13-rpg-vertical-slice-phase01-07-design.md`

---

## Global Constraints

以下约束对**每一个 Task** 都生效，不再逐条重复。

1. **Unity 版本**：2022.3.62f3c1，Built-in 渲染管线。不引入 URP/HDRP，不装新包。
2. **不生成、不修改任何 Animator Controller。** 主角与怪物的状态机由用户实现。本计划只按既定契约驱动它们：
   - 主角参数：`Speed`(float)、`IsDefend`/`IsRoll`(bool)、`Attack`/`Jump`/`GetHit`/`Die`(trigger)
   - 主角动画事件：`AttackStart`、`AttackEnd`、`JumpStart`、`JumpEnd`、`HitEnd`
   - 怪物参数：`Speed`(float)、`Attack`/`GetHit`/`Die`(trigger)
   - 怪物动画事件：`AttackStart`、`AttackEnd`、`HitEnd`
3. **不改动 `Assets/Script/PlayerController.cs` 的既有逻辑**，唯一例外是 Task 9 中 4 行纯加法的事件声明与触发。
4. **不改动 `Assets/Script/ThirdPersonCamera.cs`。**
5. **新文件一律 `namespace Demo.<Area>`**（`Demo.Core`、`Demo.Data`、`Demo.Combat`、`Demo.Player`、`Demo.Enemy`、`Demo.Interaction`、`Demo.Quest`、`Demo.Dialogue`、`Demo.Drop`、`Demo.Debugging`）。两个既有脚本保留在全局命名空间，**不加 namespace**，避免无谓改动。
   - 注意 `Debug/` 目录下的类必须用 `Demo.Debugging` 而非 `Demo.Debug`：命名空间 `Demo.Debug` 会让 `Debug.Log` 解析到该命名空间本身而非 `UnityEngine.Debug`，造成编译错误。
6. **新建 `.cs` 文件时，文件内容第一个字符必须是 U+FEFF**，以写入 UTF-8 BOM。原因：项目中文注释 + 中文 Windows 上的 VS 默认按 GBK 解码无 BOM 文件，会产生乱码。既有两个文件不转码。
7. **不使用 `FindObjectsOfType` / `FindObjectOfType` 做系统间通信。** 唯一允许的查找是 Task 11 中 `EnemyAI` 在 `Start()` 里对 Tag `Player` 的一次性兜底查找。
8. **不使用 Tag、不使用 Layer 做游戏逻辑判定。** 不需要修改 `ProjectSettings/TagManager.asset`。
9. **所有 `EventBus` 订阅严格成对：`OnEnable` 订阅 / `OnDisable` 退订。** 无例外。
10. **项目不是 git 仓库。** 每个 Task 结束时做一次快照代替 commit：

```bash
cd "E:/unity/求职demo" && D="docs/superpowers/sdd/snaps/task<N>-final" && rm -rf "$D" && mkdir -p "$D/Assets" && cp -r Assets/Script "$D/Assets/" && { [ -d Assets/Tests ] && cp -r Assets/Tests "$D/Assets/" || true; }
```

11. **运行 Unity 前必须先关闭 Unity 编辑器。** Unity batchmode 无法打开已被编辑器占用的项目，会直接失败。若失败，第一件事是确认编辑器已关闭。
12. **编译检查命令**（每写完一批代码都要跑）：

```bash
cd "E:/unity/求职demo" && "/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath "E:/unity/求职demo" -logFile "E:/unity/求职demo/Logs/compile.log"; echo "EXIT=$?"; grep -nE "error CS" Logs/compile.log || echo "无编译错误"
```

13. **EditMode 测试命令**：

```bash
cd "E:/unity/求职demo" && "/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" -batchmode -nographics -projectPath "E:/unity/求职demo" -runTests -testPlatform EditMode -testResults "E:/unity/求职demo/Logs/editmode-results.xml" -logFile "E:/unity/求职demo/Logs/editmode.log"; echo "EXIT=$?"
```

退出码 0 = 全部通过；非 0 = 有用例失败，详情读 `Logs/editmode-results.xml`。**注意此处不加 `-quit`**，Test Runner 跑完会自行退出。

14. **测试必须互不污染。** 每个测试类的 `[SetUp]` 里必须调用 `EventBus.Clear()` 与 `InputLock.Clear()`。
15. **不删除 `docs/` 下任何既有文件。**

---

## 文件结构

### 新建

| 路径 | 职责 |
|---|---|
| `Assets/Script/Demo.Runtime.asmdef` | 运行时程序集 |
| `Assets/Script/Core/EventBus.cs` | 静态类型安全发布订阅 |
| `Assets/Script/Core/InputLock.cs` | 按持有者的输入锁 |
| `Assets/Script/Core/GameEvents.cs` | 全部事件结构体 |
| `Assets/Script/Core/DamageInfo.cs` | 伤害数据结构 |
| `Assets/Script/Core/IDamageable.cs` | 可受伤接口 |
| `Assets/Script/Data/ItemData.cs` | 物品基类 + `ItemType` 枚举 |
| `Assets/Script/Data/WeaponData.cs` | 武器数据 |
| `Assets/Script/Data/ConsumableData.cs` | 消耗品数据 |
| `Assets/Script/Data/QuestItemData.cs` | 任务物品数据 |
| `Assets/Script/Data/QuestData.cs` | 任务数据 |
| `Assets/Script/Data/DialogueData.cs` | 对话数据 + `DialogueOption` |
| `Assets/Script/Interaction/IInteractable.cs` | 可交互接口 |
| `Assets/Script/Interaction/NpcInteractable.cs` | NPC 交互 |
| `Assets/Script/Interaction/DroppedItem.cs` | 掉落物交互 |
| `Assets/Script/Player/PlayerInteractionDetector.cs` | 玩家交互检测 |
| `Assets/Script/Player/PlayerStats.cs` | 攻击力 |
| `Assets/Script/Player/PlayerCombat.cs` | 攻击窗口 → Hitbox |
| `Assets/Script/Player/PlayerHealthBridge.cs` | HealthComponent → PlayerController 的受击/死亡动画 |
| `Assets/Script/Combat/HealthComponent.cs` | 血量 |
| `Assets/Script/Combat/Hitbox.cs` | 攻击判定 |
| `Assets/Script/Combat/DamageCalculator.cs` | 最终攻击力计算 |
| `Assets/Script/Enemy/EnemyState.cs` | 怪物状态枚举 + 传感器/意图结构体 |
| `Assets/Script/Enemy/EnemyBrain.cs` | 纯逻辑状态机 |
| `Assets/Script/Enemy/EnemyAI.cs` | 怪物桥接 |
| `Assets/Script/Quest/QuestStatus.cs` | 任务状态枚举 |
| `Assets/Script/Quest/QuestSystem.cs` | 纯逻辑任务系统 |
| `Assets/Script/Quest/QuestComponent.cs` | 任务桥接 |
| `Assets/Script/Dialogue/DialogueSystem.cs` | 纯逻辑对话系统 |
| `Assets/Script/Dialogue/DialogueRunner.cs` | 对话桥接 |
| `Assets/Script/Drop/DropSpawner.cs` | 掉落生成 |
| `Assets/Script/Debug/DebugHud.cs` | 临时 IMGUI 调试 HUD |
| `Assets/Tests/EditMode/Demo.Tests.EditMode.asmdef` | 测试程序集 |
| `Assets/Tests/EditMode/EventBusTests.cs` | |
| `Assets/Tests/EditMode/InputLockTests.cs` | |
| `Assets/Tests/EditMode/DataAssetTests.cs` | |
| `Assets/Tests/EditMode/DialogueSystemTests.cs` | |
| `Assets/Tests/EditMode/QuestSystemTests.cs` | |
| `Assets/Tests/EditMode/DamageCalculatorTests.cs` | |
| `Assets/Tests/EditMode/EnemyBrainTests.cs` | |

### 修改

| 路径 | 改动 |
|---|---|
| `Assets/Script/PlayerController.cs` | Task 9：加 2 个 event 声明 + 2 行 Invoke（4 行，纯加法，行为零变化） |

---

## Task 1: 程序集骨架 + EventBus + InputLock

**Phase 01。** 这个 Task 同时承担"证明命令行编译与测试环路可用"的职责。asmdef 是测试的前提，因此与首批代码放在同一 Task。

**Files:**
- Create: `Assets/Script/Demo.Runtime.asmdef`
- Create: `Assets/Script/Core/EventBus.cs`
- Create: `Assets/Script/Core/InputLock.cs`
- Create: `Assets/Tests/EditMode/Demo.Tests.EditMode.asmdef`
- Test: `Assets/Tests/EditMode/EventBusTests.cs`
- Test: `Assets/Tests/EditMode/InputLockTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:
  - `Demo.Core.EventBus.Subscribe<T>(Action<T>) where T : struct`
  - `Demo.Core.EventBus.Unsubscribe<T>(Action<T>) where T : struct`
  - `Demo.Core.EventBus.Publish<T>(T) where T : struct`
  - `Demo.Core.EventBus.Clear()`
  - `Demo.Core.InputLock.IsLocked` (bool)
  - `Demo.Core.InputLock.Acquire(object)` / `Release(object)` / `Clear()`

- [ ] **Step 1: 创建运行时程序集定义**

`Assets/Script/Demo.Runtime.asmdef`（**不含 BOM**，JSON 文件）：

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

- [ ] **Step 2: 创建测试程序集定义**

`Assets/Tests/EditMode/Demo.Tests.EditMode.asmdef`（**不含 BOM**）：

```json
{
    "name": "Demo.Tests.EditMode",
    "rootNamespace": "Demo.Tests",
    "references": [
        "Demo.Runtime",
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

- [ ] **Step 3: 写失败的测试**

`Assets/Tests/EditMode/EventBusTests.cs`（内容以 U+FEFF 开头）：

```csharp
using System;
using NUnit.Framework;
using Demo.Core;

namespace Demo.Tests
{
    public class EventBusTests
    {
        private struct Ping { public int Value; }
        private struct Pong { public string Text; }

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();
        }

        [Test]
        public void Subscribe_ThenPublish_ReceivesEvent()
        {
            int received = 0;
            Action<Ping> handler = e => received = e.Value;

            EventBus.Subscribe(handler);
            EventBus.Publish(new Ping { Value = 42 });

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Unsubscribe_ThenPublish_DoesNotReceive()
        {
            int callCount = 0;
            Action<Ping> handler = e => callCount++;

            EventBus.Subscribe(handler);
            EventBus.Unsubscribe(handler);
            EventBus.Publish(new Ping { Value = 1 });

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void Publish_OnlyNotifiesMatchingType()
        {
            int pingCount = 0;
            int pongCount = 0;

            Action<Ping> onPing = e => pingCount++;
            Action<Pong> onPong = e => pongCount++;

            EventBus.Subscribe(onPing);
            EventBus.Subscribe(onPong);
            EventBus.Publish(new Ping { Value = 7 });

            Assert.AreEqual(1, pingCount);
            Assert.AreEqual(0, pongCount);
        }

        [Test]
        public void Publish_WithMultipleSubscribers_NotifiesAll()
        {
            int a = 0;
            int b = 0;
            Action<Ping> ha = e => a++;
            Action<Ping> hb = e => b++;

            EventBus.Subscribe(ha);
            EventBus.Subscribe(hb);
            EventBus.Publish(new Ping { Value = 1 });

            Assert.AreEqual(1, a);
            Assert.AreEqual(1, b);
        }

        [Test]
        public void Publish_WhenHandlerUnsubscribesDuringDispatch_DoesNotThrow()
        {
            Action<Ping> handler = null;
            handler = e => EventBus.Unsubscribe(handler);

            EventBus.Subscribe(handler);

            Assert.DoesNotThrow(() => EventBus.Publish(new Ping { Value = 1 }));
        }

        [Test]
        public void Clear_RemovesAllSubscribers()
        {
            int callCount = 0;
            Action<Ping> handler = e => callCount++;

            EventBus.Subscribe(handler);
            EventBus.Clear();
            EventBus.Publish(new Ping { Value = 1 });

            Assert.AreEqual(0, callCount);
        }
    }
}
```

`Assets/Tests/EditMode/InputLockTests.cs`（内容以 U+FEFF 开头）：

```csharp
using NUnit.Framework;
using Demo.Core;

namespace Demo.Tests
{
    public class InputLockTests
    {
        [SetUp]
        public void SetUp()
        {
            InputLock.Clear();
        }

        [Test]
        public void Initially_IsNotLocked()
        {
            Assert.IsFalse(InputLock.IsLocked);
        }

        [Test]
        public void Acquire_Locks()
        {
            object owner = new object();
            InputLock.Acquire(owner);

            Assert.IsTrue(InputLock.IsLocked);
        }

        [Test]
        public void Release_Unlocks()
        {
            object owner = new object();
            InputLock.Acquire(owner);
            InputLock.Release(owner);

            Assert.IsFalse(InputLock.IsLocked);
        }

        [Test]
        public void TwoOwners_OneReleases_RemainsLocked()
        {
            object a = new object();
            object b = new object();

            InputLock.Acquire(a);
            InputLock.Acquire(b);
            InputLock.Release(a);

            Assert.IsTrue(InputLock.IsLocked);
        }

        [Test]
        public void Release_FromNonOwner_DoesNotUnlock()
        {
            object owner = new object();
            object stranger = new object();

            InputLock.Acquire(owner);
            InputLock.Release(stranger);

            Assert.IsTrue(InputLock.IsLocked);
        }

        [Test]
        public void DoubleRelease_DoesNotThrow()
        {
            object owner = new object();
            InputLock.Acquire(owner);

            Assert.DoesNotThrow(() =>
            {
                InputLock.Release(owner);
                InputLock.Release(owner);
            });
        }
    }
}
```

- [ ] **Step 4: 运行测试确认失败**

先关闭 Unity 编辑器，然后运行 Global Constraints 第 13 条的测试命令。
预期：非 0 退出码，因为 `Demo.Core.EventBus` 尚不存在，`Demo.Runtime` 编译失败。

- [ ] **Step 5: 实现 EventBus**

`Assets/Script/Core/EventBus.cs`（内容以 U+FEFF 开头）：

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo.Core
{
    /// <summary>
    /// 静态类型安全的发布订阅总线。事件一律为值类型结构体。
    /// 订阅方必须在 OnDisable 中成对退订。
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Handlers =
            new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                return;
            }

            Type key = typeof(T);

            if (Handlers.TryGetValue(key, out Delegate existing))
            {
                Handlers[key] = Delegate.Combine(existing, handler);
            }
            else
            {
                Handlers[key] = handler;
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                return;
            }

            Type key = typeof(T);

            if (!Handlers.TryGetValue(key, out Delegate existing))
            {
                return;
            }

            Delegate remaining = Delegate.Remove(existing, handler);

            if (remaining == null)
            {
                Handlers.Remove(key);
            }
            else
            {
                Handlers[key] = remaining;
            }
        }

        public static void Publish<T>(T evt) where T : struct
        {
            if (!Handlers.TryGetValue(typeof(T), out Delegate existing))
            {
                return;
            }

            // 取快照后调用：处理器在回调中订阅/退订时不会破坏本次派发。
            Delegate[] snapshot = existing.GetInvocationList();

            for (int i = 0; i < snapshot.Length; i++)
            {
                ((Action<T>)snapshot[i]).Invoke(evt);
            }
        }

        public static void Clear()
        {
            Handlers.Clear();
        }

        // 关闭 Domain Reload 时静态状态会跨 Play 残留，必须清空。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Clear();
        }
    }
}
```

- [ ] **Step 6: 实现 InputLock**

`Assets/Script/Core/InputLock.cs`（内容以 U+FEFF 开头）：

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Demo.Core
{
    /// <summary>
    /// 按持有者计数的输入锁。对话等模态状态下屏蔽攻击/交互输入。
    /// </summary>
    public static class InputLock
    {
        private static readonly HashSet<object> Owners = new HashSet<object>();

        public static bool IsLocked
        {
            get { return Owners.Count > 0; }
        }

        public static void Acquire(object owner)
        {
            if (owner == null)
            {
                return;
            }

            Owners.Add(owner);
        }

        public static void Release(object owner)
        {
            if (owner == null)
            {
                return;
            }

            Owners.Remove(owner);
        }

        public static void Clear()
        {
            Owners.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Clear();
        }
    }
}
```

- [ ] **Step 7: 编译检查**

运行 Global Constraints 第 12 条命令。
预期：`EXIT=0`，输出 `无编译错误`。

- [ ] **Step 8: 运行测试确认通过**

关闭 Unity 编辑器，运行 Global Constraints 第 13 条命令。
预期：`EXIT=0`，`EventBusTests` 6 个用例 + `InputLockTests` 6 个用例全部通过。

**如果失败**：先确认 Unity 编辑器已关闭；再看 `Logs/editmode.log` 中是否有 asmdef 引用错误（常见为 `Demo.Runtime` 名字拼写不符）。

- [ ] **Step 9: 快照**

运行 Global Constraints 第 10 条命令，`<N>` = `1`。

---

## Task 2: 事件契约 + 数据层 ScriptableObject

**Phase 01。** `GameEvents` 是后续所有 Task 的接口，必须一次定义完整。

**Files:**
- Create: `Assets/Script/Core/GameEvents.cs`
- Create: `Assets/Script/Data/ItemData.cs`
- Create: `Assets/Script/Data/WeaponData.cs`
- Create: `Assets/Script/Data/ConsumableData.cs`
- Create: `Assets/Script/Data/QuestItemData.cs`
- Create: `Assets/Script/Data/QuestData.cs`
- Create: `Assets/Script/Data/DialogueData.cs`
- Test: `Assets/Tests/EditMode/DataAssetTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:
  - `Demo.Core.InteractionPromptChangedEvent { string PromptText; bool Visible; }`

```csharp
public readonly struct InteractionPromptChangedEvent
{
    public readonly string PromptText;
    public readonly bool Visible;
    public InteractionPromptChangedEvent(string promptText, bool visible);
}
```

  - `Demo.Core.InteractedEvent { string TargetName; }`
  - `Demo.Core.DialogueStartedEvent { DialogueData Data; }`
  - `Demo.Core.DialogueLineChangedEvent { string Speaker; string Line; int Index; int Total; }`
  - `Demo.Core.DialogueEndedEvent { DialogueData Data; }`
  - `Demo.Core.QuestAcceptedEvent { QuestData Quest; }`
  - `Demo.Core.QuestProgressChangedEvent { QuestData Quest; int Current; int Required; }`
  - `Demo.Core.QuestCompletedEvent { QuestData Quest; }`
  - `Demo.Core.EntityDamagedEvent { string TargetName; float Amount; float RemainingHp; bool IsPlayer; }`
  - `Demo.Core.EnemyDiedEvent { string EnemyTypeId; Vector3 Position; WeaponData Drop; }`
  - `Demo.Core.ItemPickedUpEvent { ItemData Item; int Amount; }`
  - `Demo.Data.ItemType { Weapon, Consumable, Quest }`
  - `Demo.Data.ItemData : ScriptableObject`，字段 `id`/`displayName`/`description`/`icon`/`maxStack`/`itemType`
  - `Demo.Data.WeaponData : ItemData`，新增 `damage`/`weaponPrefab`/`socketLocalPosition`/`socketLocalEuler`/`socketLocalScale`
  - `Demo.Data.ConsumableData : ItemData`，新增 `healAmount`
  - `Demo.Data.QuestItemData : ItemData`
  - `Demo.Data.QuestData : ScriptableObject`，字段 `id`/`title`/`description`/`objectiveText`/`requiredAmount`/`targetEnemyTypeId`/`rewardItem`/`rewardAmount`
  - `Demo.Data.DialogueOption`（`[Serializable]` 普通类），字段 `text`/`nextDialogue`/`questToOffer`
  - `Demo.Data.DialogueData : ScriptableObject`，字段 `speakerName`/`lines`/`options`

- [ ] **Step 1: 写失败的测试**

`Assets/Tests/EditMode/DataAssetTests.cs`（内容以 U+FEFF 开头）：

```csharp
using NUnit.Framework;
using UnityEngine;
using Demo.Core;
using Demo.Data;

namespace Demo.Tests
{
    public class DataAssetTests
    {
        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();
        }

        [Test]
        public void WeaponData_IsAssignableToItemDataField()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            ItemData asBase = weapon;

            Assert.IsNotNull(asBase);
            Assert.AreEqual(ItemType.Weapon, asBase.itemType);
        }

        [Test]
        public void WeaponData_Defaults()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();

            Assert.AreEqual(1, weapon.maxStack);
            Assert.AreEqual(Vector3.one, weapon.socketLocalScale);
            Assert.AreEqual(0, weapon.damage);
        }

        [Test]
        public void QuestData_DefaultsToThreeRequired()
        {
            QuestData quest = ScriptableObject.CreateInstance<QuestData>();

            Assert.AreEqual(3, quest.requiredAmount);
            Assert.IsNullOrEmpty(quest.targetEnemyTypeId);
        }

        [Test]
        public void DialogueData_EmptyOptionsByDefault()
        {
            DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();

            Assert.IsNull(dialogue.options);
            Assert.IsNull(dialogue.lines);
        }

        [Test]
        public void EnemyDiedEvent_CarriesDropPayload()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            var evt = new EnemyDiedEvent("LizardWarrior", new Vector3(1f, 2f, 3f), weapon);

            Assert.AreEqual("LizardWarrior", evt.EnemyTypeId);
            Assert.AreEqual(3f, evt.Position.z);
            Assert.AreSame(weapon, evt.Drop);
        }

        [Test]
        public void InteractionPromptChangedEvent_HoldsTextAndVisibility()
        {
            var evt = new InteractionPromptChangedEvent("对话", true);

            Assert.AreEqual("对话", evt.PromptText);
            Assert.IsTrue(evt.Visible);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

关闭 Unity 编辑器，运行测试命令。
预期：非 0 退出码，`Demo.Core.EnemyDiedEvent` 等类型不存在导致编译失败。

- [ ] **Step 3: 实现 GameEvents**

`Assets/Script/Core/GameEvents.cs`（内容以 U+FEFF 开头）：

```csharp
using UnityEngine;
using Demo.Data;

namespace Demo.Core
{
    /// <summary>交互提示变化。PromptText 不含按键前缀，UI 自行拼 "[E] "。</summary>
    public readonly struct InteractionPromptChangedEvent
    {
        public readonly string PromptText;
        public readonly bool Visible;

        public InteractionPromptChangedEvent(string promptText, bool visible)
        {
            PromptText = promptText;
            Visible = visible;
        }
    }

    /// <summary>玩家完成了一次交互。</summary>
    public readonly struct InteractedEvent
    {
        public readonly string TargetName;

        public InteractedEvent(string targetName)
        {
            TargetName = targetName;
        }
    }

    public readonly struct DialogueStartedEvent
    {
        public readonly DialogueData Data;

        public DialogueStartedEvent(DialogueData data)
        {
            Data = data;
        }
    }

    public readonly struct DialogueLineChangedEvent
    {
        public readonly string Speaker;
        public readonly string Line;
        public readonly int Index;
        public readonly int Total;

        public DialogueLineChangedEvent(string speaker, string line, int index, int total)
        {
            Speaker = speaker;
            Line = line;
            Index = index;
            Total = total;
        }
    }

    public readonly struct DialogueEndedEvent
    {
        public readonly DialogueData Data;

        public DialogueEndedEvent(DialogueData data)
        {
            Data = data;
        }
    }

    public readonly struct QuestAcceptedEvent
    {
        public readonly QuestData Quest;

        public QuestAcceptedEvent(QuestData quest)
        {
            Quest = quest;
        }
    }

    public readonly struct QuestProgressChangedEvent
    {
        public readonly QuestData Quest;
        public readonly int Current;
        public readonly int Required;

        public QuestProgressChangedEvent(QuestData quest, int current, int required)
        {
            Quest = quest;
            Current = current;
            Required = required;
        }
    }

    public readonly struct QuestCompletedEvent
    {
        public readonly QuestData Quest;

        public QuestCompletedEvent(QuestData quest)
        {
            Quest = quest;
        }
    }

    public readonly struct EntityDamagedEvent
    {
        public readonly string TargetName;
        public readonly float Amount;
        public readonly float RemainingHp;
        public readonly bool IsPlayer;

        public EntityDamagedEvent(string targetName, float amount, float remainingHp, bool isPlayer)
        {
            TargetName = targetName;
            Amount = amount;
            RemainingHp = remainingHp;
            IsPlayer = isPlayer;
        }
    }

    /// <summary>怪物死亡。Enemy 只发布此事件，不引用 QuestSystem 或 DropSpawner。</summary>
    public readonly struct EnemyDiedEvent
    {
        public readonly string EnemyTypeId;
        public readonly Vector3 Position;
        public readonly WeaponData Drop;

        public EnemyDiedEvent(string enemyTypeId, Vector3 position, WeaponData drop)
        {
            EnemyTypeId = enemyTypeId;
            Position = position;
            Drop = drop;
        }
    }

    public readonly struct ItemPickedUpEvent
    {
        public readonly ItemData Item;
        public readonly int Amount;

        public ItemPickedUpEvent(ItemData item, int amount)
        {
            Item = item;
            Amount = amount;
        }
    }
}
```

- [ ] **Step 4: 实现物品数据**

`Assets/Script/Data/ItemData.cs`：

```csharp
using UnityEngine;

namespace Demo.Data
{
    public enum ItemType
    {
        Weapon,
        Consumable,
        Quest
    }

    public abstract class ItemData : ScriptableObject
    {
        public string id;
        public string displayName;

        [TextArea]
        public string description;

        public Sprite icon;
        public int maxStack = 1;
        public ItemType itemType;
    }
}
```

`Assets/Script/Data/WeaponData.cs`：

```csharp
using UnityEngine;

namespace Demo.Data
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Demo/Weapon Data")]
    public class WeaponData : ItemData
    {
        [Header("战斗")]
        public int damage;

        [Header("模型")]
        [Tooltip("手持模型与掉落物模型共用同一个 prefab")]
        public GameObject weaponPrefab;

        [Header("挂到 WeaponSocket 时的本地变换")]
        public Vector3 socketLocalPosition;
        public Vector3 socketLocalEuler;
        public Vector3 socketLocalScale = Vector3.one;

        private void OnValidate()
        {
            itemType = ItemType.Weapon;
        }
    }
}
```

`Assets/Script/Data/ConsumableData.cs`：

```csharp
using UnityEngine;

namespace Demo.Data
{
    [CreateAssetMenu(fileName = "ConsumableData", menuName = "Demo/Consumable Data")]
    public class ConsumableData : ItemData
    {
        public int healAmount;

        private void OnValidate()
        {
            itemType = ItemType.Consumable;
        }
    }
}
```

`Assets/Script/Data/QuestItemData.cs`：

```csharp
using UnityEngine;

namespace Demo.Data
{
    [CreateAssetMenu(fileName = "QuestItemData", menuName = "Demo/Quest Item Data")]
    public class QuestItemData : ItemData
    {
        private void OnValidate()
        {
            itemType = ItemType.Quest;
        }
    }
}
```

- [ ] **Step 5: 实现任务与对话数据**

`Assets/Script/Data/QuestData.cs`：

```csharp
using UnityEngine;

namespace Demo.Data
{
    [CreateAssetMenu(fileName = "QuestData", menuName = "Demo/Quest Data")]
    public class QuestData : ScriptableObject
    {
        [Header("标识")]
        [Tooltip("如 KillMonsters")]
        public string id;

        public string title;

        [TextArea]
        public string description;

        [Header("目标")]
        [TextArea]
        public string objectiveText;

        public int requiredAmount = 3;

        [Tooltip("留空 = 任意敌人都计数")]
        public string targetEnemyTypeId;

        [Header("奖励（本切片不发放）")]
        public ItemData rewardItem;
        public int rewardAmount;
    }
}
```

`Assets/Script/Data/DialogueData.cs`：

```csharp
using System;
using UnityEngine;

namespace Demo.Data
{
    [Serializable]
    public class DialogueOption
    {
        public string text;

        [Tooltip("留空 = 结束对话")]
        public DialogueData nextDialogue;

        [Tooltip("非空 = 选中此项即接受该任务")]
        public QuestData questToOffer;
    }

    [CreateAssetMenu(fileName = "DialogueData", menuName = "Demo/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        public string speakerName;

        [TextArea]
        public string[] lines;

        [Tooltip("在最后一句之后显示；留空 = 说完即结束")]
        public DialogueOption[] options;
    }
}
```

- [ ] **Step 6: 编译检查**

运行 Global Constraints 第 12 条命令。预期 `EXIT=0`，`无编译错误`。

- [ ] **Step 7: 运行测试确认通过**

关闭 Unity 编辑器，运行测试命令。预期 `EXIT=0`，`DataAssetTests` 6 个用例全通过。

- [ ] **Step 8: 快照**（`<N>` = `2`）

---

## Task 3: Interaction + 调试 HUD

**Phase 02。** HUD 在此建立，因为 Phase 02 的验收标准（"看到 E 提示"）依赖它。

**Files:**
- Create: `Assets/Script/Interaction/IInteractable.cs`
- Create: `Assets/Script/Interaction/NpcInteractable.cs`
- Create: `Assets/Script/Player/PlayerInteractionDetector.cs`
- Create: `Assets/Script/Debug/DebugHud.cs`

**Interfaces:**
- Consumes: `EventBus`、`InputLock`、`InteractionPromptChangedEvent`、`InteractedEvent`
- Produces:
  - `Demo.Interaction.IInteractable { string PromptText { get; } bool CanInteract(GameObject); void Interact(GameObject); Transform Transform { get; } }`
  - `Demo.Interaction.NpcInteractable`，公开字段 `displayName`；`public void SetRunner(DialogueRunner runner)` 留待 Task 5 使用
  - `Demo.Player.PlayerInteractionDetector`

- [ ] **Step 1: 实现 IInteractable**

无独立测试（纯接口，由 Task 5 与 Task 12 的行为测试间接覆盖）。

`Assets/Script/Interaction/IInteractable.cs`（内容以 U+FEFF 开头）：

```csharp
using UnityEngine;

namespace Demo.Interaction
{
    /// <summary>
    /// 可交互对象。PromptText 只返回动词（"对话"/"拾取"），
    /// 按键前缀 "[E] " 由 UI 拼接。
    /// </summary>
    public interface IInteractable
    {
        string PromptText { get; }
        Transform Transform { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
```

- [ ] **Step 2: 实现 NpcInteractable（Phase 02 形态）**

本步只实现"发布 `InteractedEvent`"。Task 5 会扩展它调用 `DialogueRunner`。

`Assets/Script/Interaction/NpcInteractable.cs`（内容以 U+FEFF 开头）：

```csharp
using UnityEngine;
using Demo.Core;

namespace Demo.Interaction
{
    /// <summary>
    /// NPC 交互。Phase 02 只广播交互事件；
    /// Task 5 接入 DialogueRunner 后由 runner 播放对话。
    /// </summary>
    public class NpcInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string displayName = "村民";

        private Dialogue.DialogueRunner _runner;

        public string PromptText
        {
            get { return "对话"; }
        }

        public Transform Transform
        {
            get { return transform; }
        }

        public void SetRunner(Dialogue.DialogueRunner runner)
        {
            _runner = runner;
        }

        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled;
        }

        public void Interact(GameObject interactor)
        {
            EventBus.Publish(new InteractedEvent(displayName));

            // Task 5 之前 _runner 为 null，此时仅停留在事件广播。
            if (_runner != null)
            {
                _runner.BeginFor(this, interactor);
            }
        }
    }
}
```

**注意**：`NpcInteractable` 引用了 `Demo.Dialogue.DialogueRunner`，而该类要到 Task 5 才创建。为了让 Task 3 结束时项目仍能编译，**本步同时创建 `Assets/Script/Dialogue/DialogueRunner.cs` 的空壳**：

```csharp
using UnityEngine;

namespace Demo.Dialogue
{
    /// <summary>Task 5 实现。此处仅为让 Task 3 可编译。</summary>
    public class DialogueRunner : MonoBehaviour
    {
        public void BeginFor(Interaction.NpcInteractable npc, GameObject interactor)
        {
        }
    }
}
```

- [ ] **Step 3: 实现 PlayerInteractionDetector**

`Assets/Script/Player/PlayerInteractionDetector.cs`（内容以 U+FEFF 开头）：

```csharp
using UnityEngine;
using Demo.Core;
using Demo.Interaction;

namespace Demo.Player
{
    /// <summary>
    /// 每帧在玩家周围做球形重叠查询，取最近的可交互对象。
    /// 不使用 Layer / Tag 过滤，靠 IInteractable 组件判定。
    /// </summary>
    public class PlayerInteractionDetector : MonoBehaviour
    {
        [SerializeField] private float radius = 2.5f;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private PlayerController controller;

        private readonly Collider[] _buffer = new Collider[32];
        private IInteractable _current;

        private void Reset()
        {
            controller = GetComponent<PlayerController>();
        }

        private void OnDisable()
        {
            SetCurrent(null);
        }

        private void Update()
        {
            if (InputLock.IsLocked || (controller != null && controller.IsDead))
            {
                SetCurrent(null);
                return;
            }

            IInteractable found = FindNearest();
            SetCurrent(found);

            if (found != null && Input.GetKeyDown(interactKey))
            {
                if (found.CanInteract(gameObject))
                {
                    found.Interact(gameObject);
                }
            }
        }

        private IInteractable FindNearest()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                _buffer);

            IInteractable best = null;
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _buffer[i];

                if (col == null)
                {
                    continue;
                }

                IInteractable candidate = col.GetComponentInParent<IInteractable>();

                if (candidate == null)
                {
                    continue;
                }

                if (!candidate.CanInteract(gameObject))
                {
                    continue;
                }

                float sqrDistance =
                    (candidate.Transform.position - transform.position).sqrMagnitude;

                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    best = candidate;
                }
            }

            return best;
        }

        private void SetCurrent(IInteractable next)
        {
            if (ReferenceEquals(_current, next))
            {
                return;
            }

            _current = next;

            if (next == null)
            {
                EventBus.Publish(new InteractionPromptChangedEvent(string.Empty, false));
            }
            else
            {
                EventBus.Publish(new InteractionPromptChangedEvent(next.PromptText, true));
            }
        }
    }
}
```

**已知可接受缺陷**：`GetComponentInParent<IInteractable>()` 每帧对缓冲区内的碰撞体调用。半径 2.5m 内碰撞体数量很小，开销可忽略。若后续成为瓶颈，再引入 LayerMask。

- [ ] **Step 4: 实现调试 HUD**

`Assets/Script/Debug/DebugHud.cs`（内容以 U+FEFF 开头）：

```csharp
using System.Collections.Generic;
using UnityEngine;
using Demo.Core;
using Demo.Data;

namespace Demo.Debugging
{
    /// <summary>
    /// 临时验收用 IMGUI HUD。Phase 11 做正式 UI 时整个 Debug/ 目录删除。
    /// 所有引用均为可选，未接入的系统显示为 "—"。
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        private const int MaxLogLines = 8;

        [SerializeField] private Combat.HealthComponent playerHealth;
        [SerializeField] private Quest.QuestComponent questComponent;

        private readonly List<string> _log = new List<string>();

        private bool _promptVisible;
        private string _promptText = string.Empty;

        private void OnEnable()
        {
            EventBus.Subscribe<InteractionPromptChangedEvent>(OnPromptChanged);
            EventBus.Subscribe<InteractedEvent>(OnInteracted);
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<DialogueLineChangedEvent>(OnDialogueLineChanged);
            EventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
            EventBus.Subscribe<QuestAcceptedEvent>(OnQuestAccepted);
            EventBus.Subscribe<QuestProgressChangedEvent>(OnQuestProgressChanged);
            EventBus.Subscribe<QuestCompletedEvent>(OnQuestCompleted);
            EventBus.Subscribe<EntityDamagedEvent>(OnEntityDamaged);
            EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
            EventBus.Subscribe<ItemPickedUpEvent>(OnItemPickedUp);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InteractionPromptChangedEvent>(OnPromptChanged);
            EventBus.Unsubscribe<InteractedEvent>(OnInteracted);
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueLineChangedEvent>(OnDialogueLineChanged);
            EventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
            EventBus.Unsubscribe<QuestAcceptedEvent>(OnQuestAccepted);
            EventBus.Unsubscribe<QuestProgressChangedEvent>(OnQuestProgressChanged);
            EventBus.Unsubscribe<QuestCompletedEvent>(OnQuestCompleted);
            EventBus.Unsubscribe<EntityDamagedEvent>(OnEntityDamaged);
            EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
            EventBus.Unsubscribe<ItemPickedUpEvent>(OnItemPickedUp);
        }

        private void Log(string line)
        {
            _log.Add(line);

            while (_log.Count > MaxLogLines)
            {
                _log.RemoveAt(0);
            }
        }

        private void OnPromptChanged(InteractionPromptChangedEvent e)
        {
            _promptVisible = e.Visible;
            _promptText = e.PromptText;
        }

        private void OnInteracted(InteractedEvent e)
        {
            Log("交互: " + e.TargetName);
        }

        private void OnDialogueStarted(DialogueStartedEvent e)
        {
            Log("对话开始: " + e.Data.speakerName);
        }

        private void OnDialogueLineChanged(DialogueLineChangedEvent e)
        {
            Log("台词 " + (e.Index + 1) + "/" + e.Total + ": " + e.Line);
        }

        private void OnDialogueEnded(DialogueEndedEvent e)
        {
            Log("对话结束");
        }

        private void OnQuestAccepted(QuestAcceptedEvent e)
        {
            Log("接受任务: " + e.Quest.title);
        }

        private void OnQuestProgressChanged(QuestProgressChangedEvent e)
        {
            Log("任务进度: " + e.Current + "/" + e.Required);
        }

        private void OnQuestCompleted(QuestCompletedEvent e)
        {
            Log("任务完成: " + e.Quest.title);
        }

        private void OnEntityDamaged(EntityDamagedEvent e)
        {
            Log("伤害 " + e.TargetName + " -" + e.Amount.ToString("0.#") +
                " (剩余 " + e.RemainingHp.ToString("0.#") + ")");
        }

        private void OnEnemyDied(EnemyDiedEvent e)
        {
            Log("死亡: " + e.EnemyTypeId);
        }

        private void OnItemPickedUp(ItemPickedUpEvent e)
        {
            Log("拾取: " + e.Item.displayName + " x" + e.Amount);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10f, 10f, 460f, 340f), GUI.skin.box);

            GUILayout.Label("=== 玩家 ===");

            if (playerHealth != null)
            {
                GUILayout.Label("HP: " + playerHealth.CurrentHp.ToString("0.#") +
                    " / " + playerHealth.MaxHp.ToString("0.#"));
            }
            else
            {
                GUILayout.Label("HP: —");
            }

            GUILayout.Label("");

            GUILayout.Label("=== 任务 ===");

            if (questComponent != null)
            {
                GUILayout.Label(questComponent.DescribeForHud());
            }
            else
            {
                GUILayout.Label("—");
            }

            GUILayout.Label("");
            GUILayout.Label("=== 事件 ===");

            for (int i = 0; i < _log.Count; i++)
            {
                GUILayout.Label(_log[i]);
            }

            GUILayout.EndArea();

            if (_promptVisible && !InputLock.IsLocked)
            {
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 60f, Screen.height - 90f, 200f, 30f),
                    "[E] " + _promptText);
            }
        }
    }
}
```

**注意**：`DebugHud` 引用了 `Combat.HealthComponent` 与 `Quest.QuestComponent`，二者分别到 Task 8、Task 7 才创建。**本步同时创建这两个空壳**：

`Assets/Script/Combat/HealthComponent.cs`：

```csharp
using UnityEngine;

namespace Demo.Combat
{
    /// <summary>Task 8 实现。此处仅为让 Task 3 可编译。</summary>
    public class HealthComponent : MonoBehaviour
    {
        public float CurrentHp { get { return 0f; } }
        public float MaxHp { get { return 0f; } }
    }
}
```

`Assets/Script/Quest/QuestComponent.cs`：

```csharp
using UnityEngine;

namespace Demo.Quest
{
    /// <summary>Task 7 实现。此处仅为让 Task 3 可编译。</summary>
    public class QuestComponent : MonoBehaviour
    {
        public string DescribeForHud()
        {
            return "—";
        }
    }
}
```

- [ ] **Step 5: 编译检查**

运行编译命令。预期 `EXIT=0`，`无编译错误`。

- [ ] **Step 6: 手动验收**

**请用户执行**（这一步需要 Unity 编辑器，先打开它）：

打开 `Desert.unity`，然后**按对象**逐项接线：

**① 主角对象**（已有 `PlayerController` + `CharacterController` 的那个）

| 操作 | 组件 |
|---|---|
| 挂上 | `PlayerInteractionDetector` |

> `Controller` 字段由 `Reset()` 自动填为主角上的 `PlayerController`；若为空请手动拖入。
> 这个组件是**交互提示的唯一发布者**，漏挂则 `[E] 对话` 永不出现。

**② NPC 对象**（新建）

| 操作 | 内容 |
|---|---|
| 拖入 | `Assets/RPGTinyHeroWavePBR/Prefab/npcCharacters/MC01.prefab`（该目录下是 `MC01`…`MC20`，任选一个），放到 `WESTERN VILLAGE` 附近 |
| 加组件 | `CapsuleCollider`（`isTrigger` **不勾选**；这些 prefab 不自带碰撞体） |
| 挂上 | `NpcInteractable` |

**③ 新建空对象 `GameSystems`**

| 操作 | 组件 |
|---|---|
| 挂上 | `DebugHud` |

**④ 进 Play，走到 NPC 旁**

**预期**：
- 屏幕中下方出现 `[E] 对话`
- 走远 → 提示消失
- 按 E → HUD 事件区出现 `交互: 村民`

**若提示不出现，按此顺序排查**（每步都是前一步的前提）：
1. 主角上有没有 `PlayerInteractionDetector`？（最常见）
2. NPC 上有没有 `CapsuleCollider` 且 `isTrigger` 未勾选？
3. `DebugHud` 的方框有没有渲染出来？没有 → `DebugHud` 未挂或未启用
4. 按 E 有没有 `交互: 村民`？有 → 断点在提示那一段，不在检测

- [ ] **Step 7: 快照**（`<N>` = `3`）

---

## Task 4: DialogueSystem（纯逻辑）

**Phase 03。**

**Files:**
- Create: `Assets/Script/Dialogue/DialogueSystem.cs`
- Test: `Assets/Tests/EditMode/DialogueSystemTests.cs`

**Interfaces:**
- Consumes: `Demo.Data.DialogueData`、`Demo.Data.DialogueOption`
- Produces:

```csharp
namespace Demo.Dialogue
{
    public class DialogueSystem
    {
        public bool IsRunning { get; }
        public string SpeakerName { get; }
        public string CurrentLine { get; }
        public int CurrentIndex { get; }
        public bool HasOptions { get; }
        public IReadOnlyList<DialogueOption> CurrentOptions { get; }
        public int StartFrame { get; }

        public void Start(DialogueData data, int currentFrame);
        public bool TryAdvance(int currentFrame);
        public bool TrySelectOption(int index, int currentFrame);

        public event Action<DialogueData> Started;
        public event Action<string, int, int> LineChanged;   // line, index, total
        public event Action<IReadOnlyList<DialogueOption>> OptionsPresented;
        public event Action<DialogueOption> OptionSelected;
        public event Action<DialogueData> Ended;
    }
}
```

- [ ] **Step 1: 写失败的测试**

`Assets/Tests/EditMode/DialogueSystemTests.cs`（内容以 U+FEFF 开头）：

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Demo.Core;
using Demo.Data;
using Demo.Dialogue;

namespace Demo.Tests
{
    public class DialogueSystemTests
    {
        private DialogueData _dialogue;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();

            _dialogue = ScriptableObject.CreateInstance<DialogueData>();
            _dialogue.speakerName = "村民";
            _dialogue.lines = new[] { "你好。", "我这里有个委托。" };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_dialogue);
        }

        [Test]
        public void Start_ShowsFirstLine()
        {
            var system = new DialogueSystem();
            system.Start(_dialogue, 100);

            Assert.IsTrue(system.IsRunning);
            Assert.AreEqual("村民", system.SpeakerName);
            Assert.AreEqual("你好。", system.CurrentLine);
            Assert.AreEqual(0, system.CurrentIndex);
            Assert.AreEqual(100, system.StartFrame);
        }

        [Test]
        public void Start_RaisesStartedEvent()
        {
            var system = new DialogueSystem();
            DialogueData received = null;
            system.Started += d => received = d;

            system.Start(_dialogue, 100);

            Assert.AreSame(_dialogue, received);
        }

        [Test]
        public void TryAdvance_MovesToNextLine()
        {
            var system = new DialogueSystem();
            system.Start(_dialogue, 100);

            bool advanced = system.TryAdvance(101);

            Assert.IsTrue(advanced);
            Assert.AreEqual("我这里有个委托。", system.CurrentLine);
            Assert.AreEqual(1, system.CurrentIndex);
        }

        [Test]
        public void TryAdvance_PastLastLine_WithNoOptions_Ends()
        {
            var system = new DialogueSystem();
            bool ended = false;
            system.Ended += d => ended = true;

            system.Start(_dialogue, 100);
            system.TryAdvance(101);
            bool advanced = system.TryAdvance(102);

            Assert.IsTrue(advanced);
            Assert.IsTrue(ended);
            Assert.IsFalse(system.IsRunning);
        }

        [Test]
        public void TryAdvance_PastLastLine_WithOptions_PresentsOptions()
        {
            var quest = ScriptableObject.CreateInstance<QuestData>();
            _dialogue.options = new[]
            {
                new DialogueOption { text = "我接受", questToOffer = quest },
                new DialogueOption { text = "再说吧" }
            };

            var system = new DialogueSystem();
            IReadOnlyList<DialogueOption> presented = null;
            system.OptionsPresented += o => presented = o;

            system.Start(_dialogue, 100);
            system.TryAdvance(101);
            system.TryAdvance(102);

            Assert.IsNotNull(presented);
            Assert.AreEqual(2, presented.Count);
            Assert.IsFalse(system.IsRunning);
            Assert.IsTrue(system.HasOptions);

            Object.DestroyImmediate(quest);
        }

        [Test]
        public void TrySelectOption_WithNextDialogue_JumpsToIt()
        {
            DialogueData next = ScriptableObject.CreateInstance<DialogueData>();
            next.speakerName = "村民";
            next.lines = new[] { "那就多谢了。" };

            _dialogue.options = new[] { new DialogueOption { text = "好", nextDialogue = next } };

            var system = new DialogueSystem();
            system.Start(_dialogue, 100);
            system.TryAdvance(101);

            bool selected = system.TrySelectOption(0, 102);

            Assert.IsTrue(selected);
            Assert.IsTrue(system.IsRunning);
            Assert.AreEqual("那就多谢了。", system.CurrentLine);

            Object.DestroyImmediate(next);
        }

        [Test]
        public void TrySelectOption_WithoutNextDialogue_Ends()
        {
            _dialogue.options = new[] { new DialogueOption { text = "算了" } };

            var system = new DialogueSystem();
            bool ended = false;
            system.Ended += d => ended = true;

            system.Start(_dialogue, 100);
            system.TryAdvance(101);
            system.TrySelectOption(0, 102);

            Assert.IsTrue(ended);
            Assert.IsFalse(system.IsRunning);
        }

        [Test]
        public void TrySelectOption_OutOfRange_ReturnsFalse()
        {
            _dialogue.options = new[] { new DialogueOption { text = "算了" } };

            var system = new DialogueSystem();
            system.Start(_dialogue, 100);
            system.TryAdvance(101);

            Assert.IsFalse(system.TrySelectOption(5, 102));
        }

        [Test]
        public void TryAdvance_OnStartFrame_IsIgnored()
        {
            var system = new DialogueSystem();
            system.Start(_dialogue, 100);

            bool advanced = system.TryAdvance(100);

            Assert.IsFalse(advanced);
            Assert.AreEqual("你好。", system.CurrentLine);
            Assert.AreEqual(0, system.CurrentIndex);
        }

        [Test]
        public void TrySelectOption_OnStartFrame_IsIgnored()
        {
            _dialogue.options = new[] { new DialogueOption { text = "算了" } };

            var system = new DialogueSystem();
            bool ended = false;
            system.Ended += d => ended = true;

            system.Start(_dialogue, 100);
            system.TryAdvance(101);

            Assert.IsFalse(system.TrySelectOption(0, 101));
            Assert.IsFalse(ended);
        }

        [Test]
        public void Start_WhileRunning_Restarts()
        {
            var system = new DialogueSystem();
            system.Start(_dialogue, 100);
            system.TryAdvance(101);

            system.Start(_dialogue, 200);

            Assert.AreEqual(0, system.CurrentIndex);
            Assert.AreEqual(200, system.StartFrame);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

关闭编辑器，运行测试命令。预期非 0 退出码，`Demo.Dialogue.DialogueSystem` 不存在。

- [ ] **Step 3: 实现 DialogueSystem**

`Assets/Script/Dialogue/DialogueSystem.cs`（内容以 U+FEFF 开头）：

```csharp
using System;
using System.Collections.Generic;
using Demo.Data;

namespace Demo.Dialogue
{
    /// <summary>
    /// 纯逻辑对话推进。不依赖任何场景对象。
    /// 帧号由调用方传入，使同帧输入守卫可在 EditMode 中直接测试。
    /// </summary>
    public class DialogueSystem
    {
        private static readonly DialogueOption[] EmptyOptions = new DialogueOption[0];

        private DialogueData _data;
        private int _index;
        private DialogueOption[] _presentedOptions = EmptyOptions;
        private bool _awaitingOption;

        public bool IsRunning { get; private set; }
        public int StartFrame { get; private set; }
        public int CurrentIndex { get { return _index; } }

        public string SpeakerName
        {
            get { return _data != null ? _data.speakerName : string.Empty; }
        }

        public string CurrentLine
        {
            get
            {
                if (_data == null || _data.lines == null)
                {
                    return string.Empty;
                }

                if (_index < 0 || _index >= _data.lines.Length)
                {
                    return string.Empty;
                }

                return _data.lines[_index];
            }
        }

        public bool HasOptions
        {
            get { return _presentedOptions.Length > 0; }
        }

        public IReadOnlyList<DialogueOption> CurrentOptions
        {
            get { return _presentedOptions; }
        }

        public event Action<DialogueData> Started;
        public event Action<string, int, int> LineChanged;
        public event Action<IReadOnlyList<DialogueOption>> OptionsPresented;
        public event Action<DialogueOption> OptionSelected;
        public event Action<DialogueData> Ended;

        public void Start(DialogueData data, int currentFrame)
        {
            _data = data;
            _index = 0;
            _presentedOptions = EmptyOptions;
            _awaitingOption = false;
            IsRunning = true;
            StartFrame = currentFrame;

            if (Started != null)
            {
                Started(_data);
            }

            RaiseLineChanged();
        }

        public bool TryAdvance(int currentFrame)
        {
            // 同帧守卫：触发交互的那个按键不能在同一帧又推进对话。
            if (!IsRunning || currentFrame == StartFrame || _awaitingOption)
            {
                return false;
            }

            int lineCount = _data != null && _data.lines != null ? _data.lines.Length : 0;

            if (_index + 1 < lineCount)
            {
                _index++;
                RaiseLineChanged();
                return true;
            }

            if (_data != null && _data.options != null && _data.options.Length > 0)
            {
                _presentedOptions = _data.options;
                _awaitingOption = true;
                IsRunning = false;

                if (OptionsPresented != null)
                {
                    OptionsPresented(_presentedOptions);
                }

                return true;
            }

            Finish();
            return true;
        }

        public bool TrySelectOption(int index, int currentFrame)
        {
            if (currentFrame == StartFrame)
            {
                return false;
            }

            if (!_awaitingOption)
            {
                return false;
            }

            if (index < 0 || index >= _presentedOptions.Length)
            {
                return false;
            }

            DialogueOption chosen = _presentedOptions[index];
            _awaitingOption = false;
            _presentedOptions = EmptyOptions;

            if (OptionSelected != null)
            {
                OptionSelected(chosen);
            }

            if (chosen.nextDialogue != null)
            {
                Start(chosen.nextDialogue, currentFrame);
                return true;
            }

            Finish();
            return true;
        }

        private void RaiseLineChanged()
        {
            if (LineChanged == null)
            {
                return;
            }

            int total = _data != null && _data.lines != null ? _data.lines.Length : 0;
            LineChanged(CurrentLine, _index, total);
        }

        private void Finish()
        {
            IsRunning = false;
            _data = null;
            _presentedOptions = EmptyOptions;
            _awaitingOption = false;

            if (Ended != null)
            {
                Ended(null);
            }
        }
    }
}
```

**注意** `Finish()` 中 `Ended(null)`：`DialogueRunner` 需要知道"刚结束的对话是否携带任务"，因此不能把 `_data` 清空后再传 null。**改为先保存再清空**——见下条修正。

- [ ] **Step 4: 修正 Finish() 传递对话数据**

`TrySelectOption` 要读取 `chosen.questToOffer`，而那发生在 `Finish()` 之前，因此 `DialogueRunner` 通过 `OptionSelected` 事件即可拿到任务。为保持 `Ended` 语义清晰，**将 `Finish()` 改为**：

```csharp
        private void Finish()
        {
            DialogueData finished = _data;

            IsRunning = false;
            _data = null;
            _presentedOptions = EmptyOptions;
            _awaitingOption = false;

            if (Ended != null)
            {
                Ended(finished);
            }
        }
```

- [ ] **Step 5: 运行测试确认通过**

关闭编辑器，运行测试命令。预期 `EXIT=0`，`DialogueSystemTests` 11 个用例全通过。

- [ ] **Step 6: 快照**（`<N>` = `4`）

---

## Task 5: DialogueRunner + NPC 对话接线

**Phase 03。**

**Files:**
- Modify: `Assets/Script/Dialogue/DialogueRunner.cs`（替换 Task 3 的空壳）
- Modify: `Assets/Script/Interaction/NpcInteractable.cs`（扩展 `BeginFor` 前的对话选取）

**Interfaces:**
- Consumes: `DialogueSystem`、`EventBus`、`InputLock`、`DialogueData`
- Produces:
  - `Demo.Dialogue.DialogueRunner.BeginFor(NpcInteractable npc, GameObject interactor)`
  - `Demo.Dialogue.DialogueRunner.IsRunning` (bool)
  - `Demo.Interaction.NpcInteractable` 新增 `[SerializeField] DialogueData dialogueBeforeQuest` / `dialogueQuestActive` / `dialogueQuestCompleted`

- [ ] **Step 1: 实现 DialogueRunner**

`Assets/Script/Dialogue/DialogueRunner.cs`（内容以 U+FEFF 开头，**整体替换**）：

```csharp
using UnityEngine;
using Demo.Core;

namespace Demo.Dialogue
{
    /// <summary>
    /// 对话桥接：把 DialogueSystem 的纯逻辑状态转成 EventBus 事件，
    /// 并在对话期间持有输入锁。
    /// 本切片用键盘推进（E / Space 下一句，1/2/3 选选项）；
    /// Phase 11 接入正式 UI 后改为鼠标点击。
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private KeyCode advanceKey = KeyCode.E;
        [SerializeField] private KeyCode alternateAdvanceKey = KeyCode.Space;

        private readonly DialogueSystem _system = new DialogueSystem();
        private Data.QuestData _pendingQuestOffer;

        public bool IsRunning
        {
            get { return _system.IsRunning || _system.HasOptions; }
        }

        private void OnEnable()
        {
            _system.Started += OnStarted;
            _system.LineChanged += OnLineChanged;
            _system.OptionsPresented += OnOptionsPresented;
            _system.OptionSelected += OnOptionSelected;
            _system.Ended += OnEnded;
        }

        private void OnDisable()
        {
            _system.Started -= OnStarted;
            _system.LineChanged -= OnLineChanged;
            _system.OptionsPresented -= OnOptionsPresented;
            _system.OptionSelected -= OnOptionSelected;
            _system.Ended -= OnEnded;

            if (_system.IsRunning || _system.HasOptions)
            {
                InputLock.Release(this);
            }
        }

        /// <summary>由 NpcInteractable 调用，播放它选定的对话。</summary>
        public void BeginFor(Interaction.NpcInteractable npc, GameObject interactor)
        {
            if (npc == null)
            {
                return;
            }

            Data.DialogueData data = npc.SelectDialogue();

            if (data == null)
            {
                return;
            }

            Begin(data);
        }

        public void Begin(Data.DialogueData data)
        {
            if (data == null)
            {
                return;
            }

            _pendingQuestOffer = null;
            InputLock.Acquire(this);
            _system.Start(data, Time.frameCount);
        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            if (_system.HasOptions)
            {
                HandleOptionInput();
                return;
            }

            if (Input.GetKeyDown(advanceKey) ||
                Input.GetKeyDown(alternateAdvanceKey))
            {
                _system.TryAdvance(Time.frameCount);
            }
        }

        private void HandleOptionInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                _system.TrySelectOption(0, Time.frameCount);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                _system.TrySelectOption(1, Time.frameCount);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                _system.TrySelectOption(2, Time.frameCount);
            }
        }

        private void OnStarted(Data.DialogueData data)
        {
            EventBus.Publish(new DialogueStartedEvent(data));
        }

        private void OnLineChanged(string line, int index, int total)
        {
            EventBus.Publish(new DialogueLineChangedEvent(_system.SpeakerName, line, index, total));
        }

        private void OnOptionsPresented(System.Collections.Generic.IReadOnlyList<Data.DialogueOption> options)
        {
            for (int i = 0; i < options.Count; i++)
            {
                EventBus.Publish(new DialogueLineChangedEvent(
                    _system.SpeakerName,
                    "[" + (i + 1) + "] " + options[i].text,
                    i,
                    options.Count));
            }
        }

        private void OnOptionSelected(Data.DialogueOption option)
        {
            if (option != null && option.questToOffer != null)
            {
                _pendingQuestOffer = option.questToOffer;
            }
        }

        private void OnEnded(Data.DialogueData data)
        {
            InputLock.Release(this);

            if (_pendingQuestOffer != null)
            {
                if (questComponent != null)
                {
                    questComponent.Accept(_pendingQuestOffer);
                }
                _pendingQuestOffer = null;
            }

            EventBus.Publish(new DialogueEndedEvent(data));
        }
    }
}
```

并在类字段区加入：

```csharp
        [Tooltip("任务组件，Inspector 里拖入")]
        [SerializeField] private Quest.QuestComponent questComponent;
```

**注意**：`questComponent` 走 Inspector 显式引用，**不做任何运行时查找**；`DialogueRunner` 与 `QuestComponent` 也不需要挂在同一个 GameObject 上。这符合架构约束第 7 条（不用 `FindObjectsOfType` 做系统间通信）。

- [ ] **Step 2: 扩展 NpcInteractable 的对话选取**

`Assets/Script/Interaction/NpcInteractable.cs`（内容以 U+FEFF 开头，**整体替换**）：

```csharp
using UnityEngine;
using Demo.Core;
using Demo.Data;

namespace Demo.Interaction
{
    /// <summary>
    /// NPC 交互。持有三段对话，按任务状态选取其一。
    /// </summary>
    public class NpcInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string displayName = "村民";

        [Header("三段对话（按任务状态选用）")]
        [SerializeField] private DialogueData dialogueBeforeQuest;
        [SerializeField] private DialogueData dialogueQuestActive;
        [SerializeField] private DialogueData dialogueQuestCompleted;

        [Header("引用")]
        [Tooltip("场景中的 DialogueRunner，Inspector 里拖入")]
        [SerializeField] private Dialogue.DialogueRunner runner;

        [Tooltip("任务组件，用于按任务状态选对话")]
        [SerializeField] private Quest.QuestComponent questComponent;

        public string PromptText
        {
            get { return "对话"; }
        }

        public Transform Transform
        {
            get { return transform; }
        }

        /// <summary>按任务状态选一段对话。都不满足时返回第一个非空的。</summary>
        public DialogueData SelectDialogue()
        {
            if (questComponent != null)
            {
                if (questComponent.IsCompleted(questComponent.PrimaryQuest))
                {
                    if (dialogueQuestCompleted != null)
                    {
                        return dialogueQuestCompleted;
                    }
                }
                else if (questComponent.IsActive(questComponent.PrimaryQuest))
                {
                    if (dialogueQuestActive != null)
                    {
                        return dialogueQuestActive;
                    }
                }
            }

            if (dialogueBeforeQuest != null)
            {
                return dialogueBeforeQuest;
            }

            if (dialogueQuestActive != null)
            {
                return dialogueQuestActive;
            }

            return dialogueQuestCompleted;
        }

        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled;
        }

        public void Interact(GameObject interactor)
        {
            EventBus.Publish(new InteractedEvent(displayName));

            if (runner != null)
            {
                runner.BeginFor(this, interactor);
            }
        }
    }
}
```

**注意**：本步引用了 `QuestComponent.IsCompleted(QuestData)` / `IsActive(QuestData)` 与 `PrimaryQuest` 属性，它们到 Task 7 才实现。**本步同时把 Task 3 建立的 `QuestComponent` 空壳扩展为**：

```csharp
using UnityEngine;

namespace Demo.Quest
{
    /// <summary>Task 7 完成实现。此处先提供 NpcInteractable 所需的查询接口。</summary>
    public class QuestComponent : MonoBehaviour
    {
        [SerializeField] private Data.QuestData primaryQuest;

        public Data.QuestData PrimaryQuest { get { return primaryQuest; } }

        public bool IsActive(Data.QuestData quest) { return false; }
        public bool IsCompleted(Data.QuestData quest) { return false; }

        public bool Accept(Data.QuestData quest) { return false; }

        public string DescribeForHud() { return "—"; }
    }
}
```

- [ ] **Step 3: 编译检查**

运行编译命令。预期 `EXIT=0`，`无编译错误`。

- [ ] **Step 4: 手动验收**

**请用户执行：**

1. 打开 `Desert.unity`
2. 建 3 个 `DialogueData` 资产（Assets 右键 → Create → Demo → Dialogue Data），填 `speakerName` 与 `lines`
3. 把 `DialogueRunner` 挂在 `GameSystems` 对象上（与 `DebugHud` 同处即可）
4. NPC 的 `NpcInteractable.runner` 拖入该 `DialogueRunner`；`NpcInteractable.dialogueBeforeQuest` 拖入第一段对话
5. 进 Play，走到 NPC 旁按 E

**预期**：对话逐句显示在 HUD 事件区；按 E 推进到结束；**对话刚开始时按下的那个 E 不会跳过第一句**；对话期间按鼠标左键不触发攻击。

- [ ] **Step 5: 快照**（`<N>` = `5`）

---

## Task 6: QuestSystem（纯逻辑）

**Phase 04。**

**Files:**
- Create: `Assets/Script/Quest/QuestStatus.cs`
- Create: `Assets/Script/Quest/QuestSystem.cs`
- Test: `Assets/Tests/EditMode/QuestSystemTests.cs`

**Interfaces:**
- Consumes: `Demo.Data.QuestData`
- Produces:

```csharp
namespace Demo.Quest
{
    public enum QuestStatus { NotStarted, InProgress, Completed }

    public class QuestSystem
    {
        public QuestStatus GetStatus(QuestData quest);
        public int GetProgress(QuestData quest);
        public bool IsActive(QuestData quest);
        public bool Accept(QuestData quest);
        public bool ReportKill(string enemyTypeId);

        public event Action<QuestData> Accepted;
        public event Action<QuestData, int, int> ProgressChanged;
        public event Action<QuestData> Completed;
    }
}
```

- [ ] **Step 1: 写失败的测试**

`Assets/Tests/EditMode/QuestSystemTests.cs`（内容以 U+FEFF 开头）：

```csharp
using NUnit.Framework;
using UnityEngine;
using Demo.Core;
using Demo.Data;
using Demo.Quest;

namespace Demo.Tests
{
    public class QuestSystemTests
    {
        private QuestData _quest;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();

            _quest = ScriptableObject.CreateInstance<QuestData>();
            _quest.id = "KillMonsters";
            _quest.title = "讨伐魔物";
            _quest.requiredAmount = 3;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_quest);
        }

        [Test]
        public void NewQuest_IsNotStarted()
        {
            var system = new QuestSystem();

            Assert.AreEqual(QuestStatus.NotStarted, system.GetStatus(_quest));
            Assert.AreEqual(0, system.GetProgress(_quest));
            Assert.IsFalse(system.IsActive(_quest));
        }

        [Test]
        public void Accept_MovesToInProgress()
        {
            var system = new QuestSystem();

            Assert.IsTrue(system.Accept(_quest));
            Assert.AreEqual(QuestStatus.InProgress, system.GetStatus(_quest));
            Assert.IsTrue(system.IsActive(_quest));
        }

        [Test]
        public void Accept_RaisesAcceptedEvent()
        {
            var system = new QuestSystem();
            QuestData received = null;
            system.Accepted += q => received = q;

            system.Accept(_quest);

            Assert.AreSame(_quest, received);
        }

        [Test]
        public void Accept_Twice_SecondReturnsFalse()
        {
            var system = new QuestSystem();

            Assert.IsTrue(system.Accept(_quest));
            Assert.IsFalse(system.Accept(_quest));
        }

        [Test]
        public void ReportKill_BeforeAccept_DoesNotCount()
        {
            var system = new QuestSystem();

            Assert.IsFalse(system.ReportKill("Werewolf"));
            Assert.AreEqual(0, system.GetProgress(_quest));
        }

        [Test]
        public void ReportKill_AfterAccept_IncrementsProgress()
        {
            var system = new QuestSystem();
            system.Accept(_quest);

            Assert.IsTrue(system.ReportKill("Werewolf"));
            Assert.AreEqual(1, system.GetProgress(_quest));
        }

        [Test]
        public void ReportKill_ReachingRequired_Completes()
        {
            var system = new QuestSystem();
            bool completed = false;
            system.Completed += q => completed = true;

            system.Accept(_quest);
            system.ReportKill("Werewolf");
            system.ReportKill("Werewolf");
            system.ReportKill("Werewolf");

            Assert.IsTrue(completed);
            Assert.AreEqual(QuestStatus.Completed, system.GetStatus(_quest));
            Assert.AreEqual(3, system.GetProgress(_quest));
        }

        [Test]
        public void ReportKill_AfterCompleted_DoesNotCountFurther()
        {
            var system = new QuestSystem();
            int completedCount = 0;
            system.Completed += q => completedCount++;

            system.Accept(_quest);
            for (int i = 0; i < 3; i++)
            {
                system.ReportKill("Werewolf");
            }

            Assert.IsFalse(system.ReportKill("Werewolf"));
            Assert.AreEqual(3, system.GetProgress(_quest));
            Assert.AreEqual(1, completedCount);
        }

        [Test]
        public void ReportKill_WithTargetFilter_IgnoresOtherTypes()
        {
            _quest.targetEnemyTypeId = "LizardWarrior";

            var system = new QuestSystem();
            system.Accept(_quest);

            Assert.IsFalse(system.ReportKill("Werewolf"));
            Assert.AreEqual(0, system.GetProgress(_quest));

            Assert.IsTrue(system.ReportKill("LizardWarrior"));
            Assert.AreEqual(1, system.GetProgress(_quest));
        }

        [Test]
        public void ProgressChanged_ReportsCurrentAndRequired()
        {
            var system = new QuestSystem();
            int lastCurrent = -1;
            int lastRequired = -1;
            system.ProgressChanged += (q, c, r) => { lastCurrent = c; lastRequired = r; };

            system.Accept(_quest);
            system.ReportKill("Werewolf");

            Assert.AreEqual(1, lastCurrent);
            Assert.AreEqual(3, lastRequired);
        }

        [Test]
        public void Accept_UnknownQuest_DoesNotThrow()
        {
            var system = new QuestSystem();

            Assert.DoesNotThrow(() => system.GetStatus(null));
            Assert.AreEqual(QuestStatus.NotStarted, system.GetStatus(null));
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

关闭编辑器，运行测试命令。预期非 0 退出码。

- [ ] **Step 3: 实现 QuestStatus**

`Assets/Script/Quest/QuestStatus.cs`（内容以 U+FEFF 开头）：

```csharp
namespace Demo.Quest
{
    public enum QuestStatus
    {
        NotStarted,
        InProgress,
        Completed
    }
}
```

- [ ] **Step 4: 实现 QuestSystem**

`Assets/Script/Quest/QuestSystem.cs`（内容以 U+FEFF 开头）：

```csharp
using System;
using System.Collections.Generic;
using Demo.Data;

namespace Demo.Quest
{
    /// <summary>
    /// 纯逻辑任务系统。不认识 Enemy、不认识场景。
    /// 击杀来源由 QuestComponent 通过 EnemyDiedEvent 转发进来。
    /// </summary>
    public class QuestSystem
    {
        private class Entry
        {
            public QuestStatus Status;
            public int Progress;
        }

        private readonly Dictionary<QuestData, Entry> _entries =
            new Dictionary<QuestData, Entry>();

        public event Action<QuestData> Accepted;
        public event Action<QuestData, int, int> ProgressChanged;
        public event Action<QuestData> Completed;

        public QuestStatus GetStatus(QuestData quest)
        {
            Entry entry = Find(quest);
            return entry != null ? entry.Status : QuestStatus.NotStarted;
        }

        public int GetProgress(QuestData quest)
        {
            Entry entry = Find(quest);
            return entry != null ? entry.Progress : 0;
        }

        public bool IsActive(QuestData quest)
        {
            return GetStatus(quest) == QuestStatus.InProgress;
        }

        public bool Accept(QuestData quest)
        {
            if (quest == null)
            {
                return false;
            }

            Entry entry = Find(quest);

            if (entry != null)
            {
                return false;
            }

            entry = new Entry { Status = QuestStatus.InProgress, Progress = 0 };
            _entries[quest] = entry;

            if (Accepted != null)
            {
                Accepted(quest);
            }

            if (ProgressChanged != null)
            {
                ProgressChanged(quest, 0, quest.requiredAmount);
            }

            return true;
        }

        public bool ReportKill(string enemyTypeId)
        {
            foreach (KeyValuePair<QuestData, Entry> pair in _entries)
            {
                QuestData quest = pair.Key;
                Entry entry = pair.Value;

                if (entry.Status != QuestStatus.InProgress)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(quest.targetEnemyTypeId) &&
                    quest.targetEnemyTypeId != enemyTypeId)
                {
                    continue;
                }

                entry.Progress++;

                if (entry.Progress >= quest.requiredAmount)
                {
                    entry.Status = QuestStatus.Completed;
                    entry.Progress = quest.requiredAmount;

                    if (ProgressChanged != null)
                    {
                        ProgressChanged(quest, entry.Progress, quest.requiredAmount);
                    }

                    if (Completed != null)
                    {
                        Completed(quest);
                    }
                }
                else if (ProgressChanged != null)
                {
                    ProgressChanged(quest, entry.Progress, quest.requiredAmount);
                }

                return true;
            }

            return false;
        }

        private Entry Find(QuestData quest)
        {
            if (quest == null)
            {
                return null;
            }

            Entry entry;
            return _entries.TryGetValue(quest, out entry) ? entry : null;
        }
    }
}
```

- [ ] **Step 5: 运行测试确认通过**

关闭编辑器，运行测试命令。预期 `EXIT=0`，`QuestSystemTests` 11 个用例全通过。

- [ ] **Step 6: 快照**（`<N>` = `6`）

---

## Task 7: QuestComponent + 接任务闭环

**Phase 04。** 本 Task 完成后，"对话 → 接任务 → HUD 显示 0/3" 闭环打通。

**Files:**
- Modify: `Assets/Script/Quest/QuestComponent.cs`（替换空壳）

**Interfaces:**
- Consumes: `QuestSystem`、`EventBus.Subscribe<EnemyDiedEvent>`、`QuestData`
- Produces:
  - `Demo.Quest.QuestComponent.PrimaryQuest` (QuestData)
  - `Demo.Quest.QuestComponent.IsActive(QuestData)` / `IsCompleted(QuestData)` / `Accept(QuestData)`
  - `Demo.Quest.QuestComponent.DescribeForHud()`

- [ ] **Step 1: 实现 QuestComponent**

`Assets/Script/Quest/QuestComponent.cs`（内容以 U+FEFF 开头，**整体替换**）：

```csharp
using UnityEngine;
using Demo.Core;

namespace Demo.Quest
{
    /// <summary>
    /// 任务桥接。订阅 EnemyDiedEvent 并转成任务进度，
    /// 再把 QuestSystem 的 C# 事件转发为 EventBus 事件供 UI 使用。
    /// Enemy 完全不知道本类的存在。
    /// </summary>
    public class QuestComponent : MonoBehaviour
    {
        [SerializeField] private Data.QuestData primaryQuest;

        private readonly QuestSystem _system = new QuestSystem();

        public Data.QuestData PrimaryQuest { get { return primaryQuest; } }

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);

            _system.Accepted += OnAccepted;
            _system.ProgressChanged += OnProgressChanged;
            _system.Completed += OnCompleted;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);

            _system.Accepted -= OnAccepted;
            _system.ProgressChanged -= OnProgressChanged;
            _system.Completed -= OnCompleted;
        }

        public bool IsActive(Data.QuestData quest)
        {
            return _system.IsActive(quest);
        }

        public bool IsCompleted(Data.QuestData quest)
        {
            return _system.GetStatus(quest) == QuestStatus.Completed;
        }

        public QuestStatus GetStatus(Data.QuestData quest)
        {
            return _system.GetStatus(quest);
        }

        public int GetProgress(Data.QuestData quest)
        {
            return _system.GetProgress(quest);
        }

        public bool Accept(Data.QuestData quest)
        {
            return _system.Accept(quest);
        }

        public string DescribeForHud()
        {
            if (primaryQuest == null)
            {
                return "未配置任务";
            }

            QuestStatus status = _system.GetStatus(primaryQuest);

            if (status == QuestStatus.NotStarted)
            {
                return primaryQuest.title + "：未接受";
            }

            return primaryQuest.title + "：" +
                _system.GetProgress(primaryQuest) + "/" + primaryQuest.requiredAmount +
                (status == QuestStatus.Completed ? "  ✔ 已完成" : "  （进行中）");
        }

        private void OnEnemyDied(EnemyDiedEvent e)
        {
            _system.ReportKill(e.EnemyTypeId);
        }

        private void OnAccepted(Data.QuestData quest)
        {
            EventBus.Publish(new QuestAcceptedEvent(quest));
        }

        private void OnProgressChanged(Data.QuestData quest, int current, int required)
        {
            EventBus.Publish(new QuestProgressChangedEvent(quest, current, required));
        }

        private void OnCompleted(Data.QuestData quest)
        {
            EventBus.Publish(new QuestCompletedEvent(quest));
        }
    }
}
```

- [ ] **Step 2: 编译检查**

运行编译命令。预期 `EXIT=0`。

- [ ] **Step 3: 手动验收**

**请用户执行：**

1. 打开 `Desert.unity`
2. 建 `QuestData` 资产：`id = KillMonsters`、`title = 讨伐魔物`、`objectiveText = 击败 3 只魔物`、`requiredAmount = 3`、`targetEnemyTypeId` **留空**
3. 建一段对话，最后加一个选项：`text = 我接受`，`questToOffer` 拖入该 `QuestData`
4. 把 `QuestComponent` 挂在 `GameSystems` 对象上（与 `DebugHud`、`DialogueRunner` 同处），`primaryQuest` 拖入第 2 步的 `QuestData`
5. `DebugHud.questComponent` 拖入它
6. `DialogueRunner.questComponent` 拖入它
7. `NpcInteractable.questComponent` 也拖入它（用于按任务状态选对话）
8. `NpcInteractable.dialogueBeforeQuest` 拖入第 3 步的对话
9. 进 Play，找 NPC 对话，选"我接受"

**预期**：HUD 任务区显示 `讨伐魔物：0/3 （进行中）`；事件区出现 `接受任务: 讨伐魔物`。此时杀怪尚未实现，进度不会变。

- [ ] **Step 4: 快照**（`<N>` = `7`）

---

## Task 8: HealthComponent + DamageCalculator + PlayerStats

**Phase 05。**

**Files:**
- Create: `Assets/Script/Core/DamageInfo.cs`
- Create: `Assets/Script/Core/IDamageable.cs`
- Create: `Assets/Script/Combat/DamageCalculator.cs`
- Modify: `Assets/Script/Combat/HealthComponent.cs`（替换空壳）
- Create: `Assets/Script/Player/PlayerStats.cs`
- Test: `Assets/Tests/EditMode/DamageCalculatorTests.cs`

**Interfaces:**
- Consumes: `EventBus`、`EntityDamagedEvent`、`WeaponData`
- Produces:
  - `Demo.Core.DamageInfo`（`readonly struct`），字段 `Amount`(float)/`Attacker`(GameObject)/`HitPoint`(Vector3)/`HitDirection`(Vector3)
  - `Demo.Core.IDamageable`：`bool IsAlive { get; }`、`Transform Transform { get; }`、`void TakeDamage(in DamageInfo)`
  - `Demo.Combat.DamageCalculator.Calculate(int baseDamage, WeaponData weapon)` → int
  - `Demo.Combat.HealthComponent`：`CurrentHp`/`MaxHp`/`IsAlive`、`event Action<DamageInfo> Damaged`、`event Action<DamageInfo> Died`、`TakeDamage(in DamageInfo)`、`ResetHealth()`
  - `Demo.Player.PlayerStats`：`BaseDamage`/`EquippedWeapon`/`FinalDamage`/`event Action StatsChanged`/`SetWeapon(WeaponData)`

- [ ] **Step 1: 写失败的测试**

`Assets/Tests/EditMode/DamageCalculatorTests.cs`（内容以 U+FEFF 开头）：

```csharp
using NUnit.Framework;
using UnityEngine;
using Demo.Core;
using Demo.Combat;
using Demo.Data;

namespace Demo.Tests
{
    public class DamageCalculatorTests
    {
        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();
        }

        [Test]
        public void NoWeapon_ReturnsBaseDamage()
        {
            Assert.AreEqual(10, DamageCalculator.Calculate(10, null));
        }

        [Test]
        public void WithWeapon_AddsWeaponDamage()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.damage = 7;

            Assert.AreEqual(17, DamageCalculator.Calculate(10, weapon));

            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void NegativeBaseDamage_ClampsToZero()
        {
            Assert.AreEqual(0, DamageCalculator.Calculate(-5, null));
        }

        [Test]
        public void NegativeWeaponDamage_ClampsToBaseDamage()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.damage = -3;

            Assert.AreEqual(10, DamageCalculator.Calculate(10, weapon));

            Object.DestroyImmediate(weapon);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

关闭编辑器，运行测试命令。预期非 0 退出码。

- [ ] **Step 3: 实现 DamageInfo 与 IDamageable**

`Assets/Script/Core/DamageInfo.cs`（内容以 U+FEFF 开头）：

```csharp
using UnityEngine;

namespace Demo.Core
{
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly GameObject Attacker;
        public readonly Vector3 HitPoint;
        public readonly Vector3 HitDirection;

        public DamageInfo(float amount, GameObject attacker, Vector3 hitPoint, Vector3 hitDirection)
        {
            Amount = amount;
            Attacker = attacker;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
        }
    }
}
```

`Assets/Script/Core/IDamageable.cs`：

```csharp
using UnityEngine;

namespace Demo.Core
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        Transform Transform { get; }
        void TakeDamage(in DamageInfo info);
    }
}
```

- [ ] **Step 4: 实现 DamageCalculator**

`Assets/Script/Combat/DamageCalculator.cs`（内容以 U+FEFF 开头）：

```csharp
using UnityEngine;
using Demo.Data;

namespace Demo.Combat
{
    /// <summary>
    /// 最终攻击力 = 基础攻击力 + 装备武器伤害。
    /// 战斗系统一律使用本方法的返回值，UI 不得自行计算。
    /// </summary>
    public static class DamageCalculator
    {
        public static int Calculate(int baseDamage, WeaponData weapon)
        {
            int basePart = Mathf.Max(0, baseDamage);
            int weaponPart = weapon != null ? Mathf.Max(0, weapon.damage) : 0;

            return basePart + weaponPart;
        }
    }
}
```

- [ ] **Step 5: 实现 HealthComponent**

`Assets/Script/Combat/HealthComponent.cs`（内容以 U+FEFF 开头，**整体替换**）：

```csharp
using System;
using UnityEngine;
using Demo.Core;

namespace Demo.Combat
{
    /// <summary>
    /// 通用血量。玩家与怪物共用。
    /// 本类不认识任务、不认识掉落物——只负责扣血与广播。
    /// </summary>
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private string displayName = "Enemy";
        [SerializeField] private bool isPlayer;

        public float MaxHp { get { return maxHp; } }
        public float CurrentHp { get; private set; }
        public bool IsPlayer { get { return isPlayer; } }

        public bool IsAlive
        {
            get { return CurrentHp > 0f; }
        }

        public Transform Transform
        {
            get { return transform; }
        }

        /// <summary>受击但未死亡。</summary>
        public event Action<DamageInfo> Damaged;

        /// <summary>死亡。只会触发一次。</summary>
        public event Action<DamageInfo> Died;

        private void Awake()
        {
            CurrentHp = maxHp;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive)
            {
                return;
            }

            CurrentHp -= info.Amount;

            if (CurrentHp <= 0f)
            {
                CurrentHp = 0f;

                EventBus.Publish(new EntityDamagedEvent(displayName, info.Amount, CurrentHp, isPlayer));

                if (Died != null)
                {
                    Died(info);
                }

                return;
            }

            EventBus.Publish(new EntityDamagedEvent(displayName, info.Amount, CurrentHp, isPlayer));

            if (Damaged != null)
            {
                Damaged(info);
            }
        }

        public void ResetHealth()
        {
            CurrentHp = maxHp;
        }
    }
}
```

**注意** `Awake()` 中初始化 `CurrentHp`：`HealthComponent` 若在运行时动态添加，`Awake` 会立即执行，行为一致。

- [ ] **Step 6: 实现 PlayerStats**

`Assets/Script/Player/PlayerStats.cs`（内容以 U+FEFF 开头）：

```csharp
using System;
using UnityEngine;
using Demo.Combat;
using Demo.Data;

namespace Demo.Player
{
    /// <summary>
    /// 玩家攻击力。Phase 09 接装备系统时通过 SetWeapon 写入武器。
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [SerializeField] private int baseDamage = 10;

        private WeaponData _equippedWeapon;

        public int BaseDamage { get { return baseDamage; } }

        public WeaponData EquippedWeapon { get { return _equippedWeapon; } }

        public int FinalDamage
        {
            get { return DamageCalculator.Calculate(baseDamage, _equippedWeapon); }
        }

        public event Action StatsChanged;

        public void SetWeapon(WeaponData weapon)
        {
            if (_equippedWeapon == weapon)
            {
                return;
            }

            _equippedWeapon = weapon;

            if (StatsChanged != null)
            {
                StatsChanged();
            }
        }
    }
}
```

- [ ] **Step 7: 运行测试确认通过**

关闭编辑器，运行测试命令。预期 `EXIT=0`，`DamageCalculatorTests` 4 个用例通过，且此前所有用例仍通过。

- [ ] **Step 8: 快照**（`<N>` = `8`）

---

## Task 9: Hitbox + PlayerCombat + PlayerController 4 行改动

**Phase 05。** 这是本计划中唯一修改既有脚本的 Task。

**Files:**
- Create: `Assets/Script/Combat/Hitbox.cs`
- Create: `Assets/Script/Player/PlayerCombat.cs`
- Create: `Assets/Script/Player/PlayerHealthBridge.cs`
- Modify: `Assets/Script/PlayerController.cs`

**Interfaces:**
- Consumes: `IDamageable`、`DamageInfo`、`PlayerStats`、`PlayerController`、`HealthComponent`
- Produces:
  - `Demo.Combat.Hitbox.OpenWindow()` / `CloseWindow()` / `IsWindowOpen` (bool)
  - `Demo.Player.PlayerCombat`
  - `Demo.Player.PlayerHealthBridge`
  - `PlayerController.AttackWindowOpened` / `AttackWindowClosed`（`event Action`）

- [ ] **Step 1: 实现 Hitbox**

`Assets/Script/Combat/Hitbox.cs`（内容以 U+FEFF 开头）：

```csharp
using System.Collections.Generic;
using UnityEngine;
using Demo.Core;

namespace Demo.Combat
{
    /// <summary>
    /// 攻击判定。窗口打开期间逐帧做球形重叠查询，
    /// 同一次窗口内对同一目标只结算一次伤害。
    ///
    /// 刻意不使用 OnTriggerEnter：触发回调依赖 Rigidbody / isTrigger /
    /// Layer 碰撞矩阵三处配置全部正确，任一处配错都会静默失效。
    /// 显式重叠查询零配置、行为确定。
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        [Tooltip("非空则使用 PlayerStats.FinalDamage，否则使用 flatDamage")]
        [SerializeField] private Player.PlayerStats stats;

        [SerializeField] private float flatDamage = 10f;
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField] private float radius = 0.8f;
        [SerializeField] private Vector3 localOffset = Vector3.zero;

        private readonly Collider[] _buffer = new Collider[16];
        private readonly HashSet<IDamageable> _hitThisWindow = new HashSet<IDamageable>();

        private bool _windowOpen;

        public bool IsWindowOpen { get { return _windowOpen; } }

        /// <summary>由动画事件触发：打开判定窗口并清空本窗口的命中记录。</summary>
        public void OpenWindow()
        {
            _hitThisWindow.Clear();
            _windowOpen = true;
        }

        /// <summary>由动画事件触发：关闭判定窗口。</summary>
        public void CloseWindow()
        {
            _windowOpen = false;
            _hitThisWindow.Clear();
        }

        private void OnDisable()
        {
            _windowOpen = false;
            _hitThisWindow.Clear();
        }

        private void Update()
        {
            if (!_windowOpen)
            {
                return;
            }

            Vector3 center = transform.TransformPoint(localOffset);

            int count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                _buffer,
                targetLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider col = _buffer[i];

                if (col == null)
                {
                    continue;
                }

                IDamageable target = col.GetComponentInParent<IDamageable>();

                if (target == null)
                {
                    continue;
                }

                if (!target.IsAlive)
                {
                    continue;
                }

                // 不伤害持有者自身
                if (target.Transform == transform.root ||
                    target.Transform == transform)
                {
                    continue;
                }

                if (_hitThisWindow.Contains(target))
                {
                    continue;
                }

                _hitThisWindow.Add(target);

                Vector3 hitPoint = col.ClosestPoint(center);
                Vector3 direction = (target.Transform.position - center).normalized;

                var info = new DamageInfo(ResolveDamage(), gameObject, hitPoint, direction);
                target.TakeDamage(info);
            }
        }

        private float ResolveDamage()
        {
            if (stats != null)
            {
                return stats.FinalDamage;
            }

            return flatDamage;
        }
    }
}
```

- [ ] **Step 2: 修改 PlayerController（4 行，纯加法）**

在 `Assets/Script/PlayerController.cs` 中做两处修改。

**修改一** —— 在 `private static readonly int DieHash = Animator.StringToHash("Die");`（约第 81-82 行）之后新增：

```csharp

    // =========================
    // 战斗窗口事件
    // =========================

    /// <summary>攻击判定窗口打开（由攻击动画的 AttackStart 帧触发）。</summary>
    public event System.Action AttackWindowOpened;

    /// <summary>攻击判定窗口关闭（由攻击动画的 AttackEnd 帧触发）。</summary>
    public event System.Action AttackWindowClosed;
```

**修改二** —— 在已有的 `AttackStart()` 方法体内（`_isAttacking = true;` 之后）新增一行：

```csharp
        if (AttackWindowOpened != null)
        {
            AttackWindowOpened();
        }
```

在已有的 `AttackEnd()` 方法体内（`_isAttacking = false;` 之后）新增一行：

```csharp
        if (AttackWindowClosed != null)
        {
            AttackWindowClosed();
        }
```

修改后这两个方法应形如：

```csharp
    public void AttackStart()
    {
        if (_isDead)
        {
            return;
        }

        _isAttacking = true;

        if (AttackWindowOpened != null)
        {
            AttackWindowOpened();
        }
    }

    public void AttackEnd()
    {
        _isAttacking = false;

        if (AttackWindowClosed != null)
        {
            AttackWindowClosed();
        }
    }
```

**行为零变化**：仅新增事件声明与触发，不改变任何既有逻辑。

- [ ] **Step 3: 实现 PlayerCombat 与 PlayerHealthBridge**

`Assets/Script/Player/PlayerCombat.cs`（内容以 U+FEFF 开头）：

```csharp
using UnityEngine;
using Demo.Combat;
using Demo.Core;

namespace Demo.Player
{
    /// <summary>
    /// 把 PlayerController 的攻击窗口事件接到 Hitbox 上。
    /// 不修改 PlayerController 的战斗职责，只订阅它广播的窗口开合。
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private Hitbox hitbox;

        private void Reset()
        {
            controller = GetComponent<PlayerController>();
            stats = GetComponent<PlayerStats>();
        }

        private void OnEnable()
        {
            if (controller == null)
            {
                controller = GetComponent<PlayerController>();
            }

            if (controller != null)
            {
                controller.AttackWindowOpened += OnWindowOpened;
                controller.AttackWindowClosed += OnWindowClosed;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.AttackWindowOpened -= OnWindowOpened;
                controller.AttackWindowClosed -= OnWindowClosed;
            }
        }

        private void OnWindowOpened()
        {
            if (InputLock.IsLocked || hitbox == null)
            {
                return;
            }

            hitbox.OpenWindow();
        }

        private void OnWindowClosed()
        {
            if (hitbox != null)
            {
                hitbox.CloseWindow();
            }
        }
    }
}
```

`Assets/Script/Player/PlayerHealthBridge.cs`（内容以 U+FEFF 开头）：

```csharp
using UnityEngine;
using Demo.Combat;
using Demo.Core;

namespace Demo.Player
{
    /// <summary>
    /// HealthComponent 与 PlayerController 之间的桥。
    /// HealthComponent 不认识 PlayerController，PlayerController 也不认识
    /// HealthComponent——受击/死亡表现由本类牵线。
    /// </summary>
    public class PlayerHealthBridge : MonoBehaviour
    {
        [SerializeField] private HealthComponent health;
        [SerializeField] private PlayerController controller;

        private void Reset()
        {
            health = GetComponent<HealthComponent>();
            controller = GetComponent<PlayerController>();
        }

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (controller == null)
            {
                controller = GetComponent<PlayerController>();
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            if (controller != null && !controller.IsDead)
            {
                controller.GetHit();
            }
        }

        private void OnDied(DamageInfo info)
        {
            if (controller != null)
            {
                controller.Die();
            }
        }
    }
}
```

**为什么必须要有这个类**：`PlayerController.GetHit()` 是受击动画的唯一入口，而 `HealthComponent` 不知道 `PlayerController` 的存在。少了它，玩家掉血但不会播放受击动画，Task 13 的验收会失败。

- [ ] **Step 4: 编译检查**

运行编译命令。预期 `EXIT=0`，`无编译错误`。

- [ ] **Step 5: 手动验收**

**请用户执行：**

1. 打开 `Desert.unity`
2. 主角对象挂 `HealthComponent`（`maxHp = 100`、`isPlayer` **勾选**、`displayName = 玩家`）、`PlayerStats`（`baseDamage = 10`）、`PlayerCombat`、`PlayerHealthBridge`
3. 主角下建子对象 `AttackHitbox`，位置调到身前约 1m、高度约 1m；挂 `Hitbox` 组件，`radius` 先设 `1.2`，`stats` 拖入主角的 `PlayerStats`，`flatDamage` 留空不用
4. `PlayerCombat` 的 `controller` / `stats` / `hitbox` 拖好
5. `DebugHud` 的 `playerHealth` 拖入主角的 `HealthComponent`
6. **临时**：给场景里随便一个物体挂 `HealthComponent`（`isPlayer` 不勾选，`displayName = 木桩`），加一个 Collider，放在主角旁边
7. 进 Play，对着木桩按鼠标左键

**预期**：
- HUD 事件区出现 `伤害 木桩 -10 (剩余 90)`
- **同一次挥砍只出现一条伤害记录**（本 Task 最关键的验证点）
- 连续挥砍可把木桩打到 0，出现 `死亡: 木桩`
- 玩家自己不会打到自己（站在原地挥砍，HUD 不出现 `伤害 玩家`）

- [ ] **Step 6: 快照**（`<N>` = `9`）

---

## Task 10: EnemyBrain（纯逻辑状态机）

**Phase 06。**

**Files:**
- Create: `Assets/Script/Enemy/EnemyState.cs`
- Create: `Assets/Script/Enemy/EnemyBrain.cs`
- Test: `Assets/Tests/EditMode/EnemyBrainTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:

```csharp
namespace Demo.Enemy
{
    public enum EnemyState { Idle, Chase, Attack, Hit, Dead }

    public readonly struct EnemySensors
    {
        public readonly bool HasTarget;
        public readonly float DistanceToTarget;
        public readonly bool HasLineOfSight;
        public EnemySensors(bool hasTarget, float distanceToTarget, bool hasLineOfSight);
    }

    public readonly struct EnemyIntents
    {
        public readonly bool Move;
        public readonly bool FaceTarget;
        public readonly bool TriggerAttack;
        public static EnemyIntents None { get; }
    }

    public class EnemyBrain
    {
        public EnemyState State { get; }
        public EnemyBrain(float detectRange, float attackRange, float leashRange, bool requireLineOfSight);
        public EnemyIntents Tick(in EnemySensors sensors);
        public void OnDamaged();
        public void OnAttackEnded();
        public void OnHitEnded();
        public void Kill();
    }
}
```

- [ ] **Step 1: 写失败的测试**

`Assets/Tests/EditMode/EnemyBrainTests.cs`（内容以 U+FEFF 开头）：

```csharp
using NUnit.Framework;
using Demo.Core;
using Demo.Enemy;

namespace Demo.Tests
{
    public class EnemyBrainTests
    {
        private const float Detect = 12f;
        private const float Attack = 2f;
        private const float Leash = 25f;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();
        }

        private static EnemyBrain MakeBrain(bool requireLineOfSight = false)
        {
            return new EnemyBrain(Detect, Attack, Leash, requireLineOfSight);
        }

        private static EnemySensors Sensors(float distance, bool hasTarget = true, bool los = true)
        {
            return new EnemySensors(hasTarget, distance, los);
        }

        [Test]
        public void StartsInIdle()
        {
            Assert.AreEqual(EnemyState.Idle, MakeBrain().State);
        }

        [Test]
        public void Idle_NoTarget_StaysIdle_WithNoIntents()
        {
            EnemyBrain brain = MakeBrain();
            EnemyIntents intents = brain.Tick(Sensors(0f, hasTarget: false));

            Assert.AreEqual(EnemyState.Idle, brain.State);
            Assert.IsFalse(intents.Move);
            Assert.IsFalse(intents.TriggerAttack);
        }

        [Test]
        public void Idle_TargetInRange_EntersChase()
        {
            EnemyBrain brain = MakeBrain();
            EnemyIntents intents = brain.Tick(Sensors(5f));

            Assert.AreEqual(EnemyState.Chase, brain.State);
            Assert.IsTrue(intents.Move);
            Assert.IsTrue(intents.FaceTarget);
        }

        [Test]
        public void Idle_TargetBeyondDetectRange_StaysIdle()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(Detect + 1f));

            Assert.AreEqual(EnemyState.Idle, brain.State);
        }

        [Test]
        public void Idle_RequireLineOfSight_AndBlocked_StaysIdle()
        {
            EnemyBrain brain = MakeBrain(requireLineOfSight: true);
            brain.Tick(Sensors(5f, los: false));

            Assert.AreEqual(EnemyState.Idle, brain.State);
        }

        [Test]
        public void Chase_WithinAttackRange_EntersAttack_AndTriggersOnce()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));

            EnemyIntents first = brain.Tick(Sensors(1.5f));

            Assert.AreEqual(EnemyState.Attack, brain.State);
            Assert.IsTrue(first.TriggerAttack);

            EnemyIntents second = brain.Tick(Sensors(1.5f));

            Assert.IsFalse(second.TriggerAttack);
        }

        [Test]
        public void Attack_DoesNotMove()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));
            brain.Tick(Sensors(1.5f));

            EnemyIntents intents = brain.Tick(Sensors(1.5f));

            Assert.IsFalse(intents.Move);
            Assert.IsTrue(intents.FaceTarget);
        }

        [Test]
        public void Attack_OnAttackEnded_ReturnsToChase()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));
            brain.Tick(Sensors(1.5f));

            brain.OnAttackEnded();

            Assert.AreEqual(EnemyState.Chase, brain.State);
        }

        [Test]
        public void Chase_BeyondLeash_ReturnsToIdle()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));

            brain.Tick(Sensors(Leash + 1f));

            Assert.AreEqual(EnemyState.Idle, brain.State);
        }

        [Test]
        public void Any_OnDamaged_EntersHit()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));

            brain.OnDamaged();

            Assert.AreEqual(EnemyState.Hit, brain.State);
        }

        [Test]
        public void Hit_DoesNotMove()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));
            brain.OnDamaged();

            EnemyIntents intents = brain.Tick(Sensors(5f));

            Assert.IsFalse(intents.Move);
            Assert.IsFalse(intents.TriggerAttack);
        }

        [Test]
        public void Hit_OnHitEnded_ReturnsToChaseWhenTargetPresent()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));
            brain.OnDamaged();

            brain.OnHitEnded();

            Assert.AreEqual(EnemyState.Chase, brain.State);
        }

        [Test]
        public void Hit_OnHitEnded_ThenTickWithoutTarget_FallsBackToIdle()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));
            brain.OnDamaged();

            brain.OnHitEnded();

            Assert.AreEqual(EnemyState.Chase, brain.State);

            brain.Tick(Sensors(0f, hasTarget: false));

            Assert.AreEqual(EnemyState.Idle, brain.State);
        }

        [Test]
        public void Kill_EntersDead()
        {
            EnemyBrain brain = MakeBrain();
            brain.Kill();

            Assert.AreEqual(EnemyState.Dead, brain.State);
        }

        [Test]
        public void Dead_IsTerminal_IgnoresDamagedAndAttackEnded()
        {
            EnemyBrain brain = MakeBrain();
            brain.Tick(Sensors(5f));
            brain.Kill();

            brain.OnDamaged();
            brain.OnAttackEnded();
            brain.OnHitEnded();
            EnemyIntents intents = brain.Tick(Sensors(1f));

            Assert.AreEqual(EnemyState.Dead, brain.State);
            Assert.IsFalse(intents.Move);
            Assert.IsFalse(intents.FaceTarget);
            Assert.IsFalse(intents.TriggerAttack);
        }

        [Test]
        public void Dead_NoTarget_StaysDead()
        {
            EnemyBrain brain = MakeBrain();
            brain.Kill();
            brain.Tick(Sensors(0f, hasTarget: false));

            Assert.AreEqual(EnemyState.Dead, brain.State);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

关闭编辑器，运行测试命令。预期非 0 退出码。

- [ ] **Step 3: 实现 EnemyState 与结构体**

`Assets/Script/Enemy/EnemyState.cs`（内容以 U+FEFF 开头）：

```csharp
namespace Demo.Enemy
{
    public enum EnemyState
    {
        Idle,
        Chase,
        Attack,
        Hit,
        Dead
    }

    public readonly struct EnemySensors
    {
        public readonly bool HasTarget;
        public readonly float DistanceToTarget;
        public readonly bool HasLineOfSight;

        public EnemySensors(bool hasTarget, float distanceToTarget, bool hasLineOfSight)
        {
            HasTarget = hasTarget;
            DistanceToTarget = distanceToTarget;
            HasLineOfSight = hasLineOfSight;
        }
    }

    public readonly struct EnemyIntents
    {
        public readonly bool Move;
        public readonly bool FaceTarget;
        public readonly bool TriggerAttack;

        public EnemyIntents(bool move, bool faceTarget, bool triggerAttack)
        {
            Move = move;
            FaceTarget = faceTarget;
            TriggerAttack = triggerAttack;
        }

        public static EnemyIntents None
        {
            get { return new EnemyIntents(false, false, false); }
        }
    }
}
```

- [ ] **Step 4: 实现 EnemyBrain**

`Assets/Script/Enemy/EnemyBrain.cs`（内容以 U+FEFF 开头）：

```csharp
namespace Demo.Enemy
{
    /// <summary>
    /// 怪物状态机纯逻辑。不依赖任何场景对象，可直接单元测试。
    /// Idle → Chase → Attack → Hit → Dead，Dead 为不可回退终态。
    /// </summary>
    public class EnemyBrain
    {
        private readonly float _detectRange;
        private readonly float _attackRange;
        private readonly float _leashRange;
        private readonly bool _requireLineOfSight;

        private bool _attackTriggerPending;

        public EnemyState State { get; private set; }

        public EnemyBrain(
            float detectRange,
            float attackRange,
            float leashRange,
            bool requireLineOfSight)
        {
            _detectRange = detectRange;
            _attackRange = attackRange;
            _leashRange = leashRange;
            _requireLineOfSight = requireLineOfSight;

            State = EnemyState.Idle;
        }

        public EnemyIntents Tick(in EnemySensors sensors)
        {
            if (State == EnemyState.Dead)
            {
                return EnemyIntents.None;
            }

            switch (State)
            {
                case EnemyState.Idle:
                    return TickIdle(sensors);

                case EnemyState.Chase:
                    return TickChase(sensors);

                case EnemyState.Attack:
                    return TickAttack(sensors);

                case EnemyState.Hit:
                    return EnemyIntents.None;
            }

            return EnemyIntents.None;
        }

        public void OnDamaged()
        {
            if (State == EnemyState.Dead)
            {
                return;
            }

            _attackTriggerPending = false;
            State = EnemyState.Hit;
        }

        public void OnAttackEnded()
        {
            if (State != EnemyState.Attack)
            {
                return;
            }

            State = EnemyState.Chase;
        }

        public void OnHitEnded()
        {
            if (State != EnemyState.Hit)
            {
                return;
            }

            State = EnemyState.Chase;
        }

        public void Kill()
        {
            if (State == EnemyState.Dead)
            {
                return;
            }

            _attackTriggerPending = false;
            State = EnemyState.Dead;
        }

        private EnemyIntents TickIdle(in EnemySensors sensors)
        {
            if (!CanDetect(sensors))
            {
                return EnemyIntents.None;
            }

            State = EnemyState.Chase;

            return new EnemyIntents(true, true, false);
        }

        private EnemyIntents TickChase(in EnemySensors sensors)
        {
            if (!sensors.HasTarget || sensors.DistanceToTarget > _leashRange)
            {
                State = EnemyState.Idle;
                return EnemyIntents.None;
            }

            if (sensors.DistanceToTarget <= _attackRange)
            {
                State = EnemyState.Attack;
                _attackTriggerPending = true;

                return new EnemyIntents(false, true, true);
            }

            return new EnemyIntents(true, true, false);
        }

        private EnemyIntents TickAttack(in EnemySensors sensors)
        {
            bool trigger = _attackTriggerPending;
            _attackTriggerPending = false;

            return new EnemyIntents(false, true, trigger);
        }

        private bool CanDetect(in EnemySensors sensors)
        {
            if (!sensors.HasTarget)
            {
                return false;
            }

            if (sensors.DistanceToTarget > _detectRange)
            {
                return false;
            }

            if (_requireLineOfSight && !sensors.HasLineOfSight)
            {
                return false;
            }

            return true;
        }
    }
}
```

**注意**：`OnHitEnded()` **一律**回到 `Chase`，不判断目标是否存在——`EnemyBrain` 不保存"受击前是什么状态"。若目标已消失，下一次 `Tick` 会因为 `!sensors.HasTarget` 把状态降回 `Idle`。这正是测试 `Hit_OnHitEnded_ThenTickWithoutTarget_FallsBackToIdle` 覆盖的两段式行为。若你希望受击结束当帧就回 Idle，需要给 `OnHitEnded` 传入目标信息，那会改变接口。

- [ ] **Step 5: 运行测试确认通过**

关闭编辑器，运行测试命令。预期 `EXIT=0`，`EnemyBrainTests` 16 个用例全通过。

- [ ] **Step 6: 快照**（`<N>` = `10`）

---

## Task 11: EnemyAI（桥接）

**Phase 06。**

**Files:**
- Create: `Assets/Script/Enemy/EnemyAI.cs`

**Interfaces:**
- Consumes: `EnemyBrain`、`EnemySensors`、`EnemyIntents`、`HealthComponent`、`Hitbox`、`EnemyDiedEvent`
- Produces: `Demo.Enemy.EnemyAI`

- [ ] **Step 1: 实现 EnemyAI**

`Assets/Script/Enemy/EnemyAI.cs`（内容以 U+FEFF 开头）：

```csharp
using UnityEngine;
using Demo.Combat;
using Demo.Core;
using Demo.Data;

namespace Demo.Enemy
{
    /// <summary>
    /// 怪物桥接：采集传感器 → 驱动 EnemyBrain → 应用意图。
    /// 死亡时只发布 EnemyDiedEvent，不引用 QuestSystem 或 DropSpawner。
    /// 移动为 transform 直接转向，不使用 NavMesh。
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private HealthComponent health;
        [SerializeField] private Animator animator;
        [SerializeField] private Hitbox attackHitbox;

        [Tooltip("留空则在 Start 中按 Tag Player 查找一次")]
        [SerializeField] private Transform playerTarget;

        [Header("参数")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float turnSpeed = 8f;
        [SerializeField] private float detectRange = 12f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float leashRange = 25f;
        [SerializeField] private bool requireLineOfSight;
        [SerializeField] private LayerMask obstacleLayers = ~0;

        [Header("身份与掉落")]
        [SerializeField] private string enemyTypeId = "Werewolf";
        [SerializeField] private WeaponData dropWeapon;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int GetHitHash = Animator.StringToHash("GetHit");
        private static readonly int DieHash = Animator.StringToHash("Die");

        private EnemyBrain _brain;
        private bool _attackAnimPlaying;
        private Vector3 _lastHitPoint;

        private void Reset()
        {
            health = GetComponent<HealthComponent>();
        }

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            _brain = new EnemyBrain(detectRange, attackRange, leashRange, requireLineOfSight);
        }

        private void Start()
        {
            if (playerTarget == null)
            {
                GameObject found = GameObject.FindGameObjectWithTag("Player");

                if (found != null)
                {
                    playerTarget = found.transform;
                }
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (animator == null || attackHitbox == null)
            {
                return;
            }

            EnemySensors sensors = GatherSensors();
            EnemyIntents intents = _brain.Tick(sensors);

            ApplyIntents(intents, sensors);
        }

        private EnemySensors GatherSensors()
        {
            if (playerTarget == null)
            {
                return new EnemySensors(false, 0f, false);
            }

            Vector3 toTarget = playerTarget.position - transform.position;
            float distance = toTarget.magnitude;

            bool los = true;

            if (requireLineOfSight && distance > 0.01f)
            {
                Vector3 origin = transform.position + Vector3.up * 1.5f;
                Vector3 target = playerTarget.position + Vector3.up * 1.5f;

                los = !Physics.Linecast(origin, target, obstacleLayers, QueryTriggerInteraction.Ignore);
            }

            return new EnemySensors(true, distance, los);
        }

        private void ApplyIntents(in EnemyIntents intents, in EnemySensors sensors)
        {
            if (intents.FaceTarget && playerTarget != null)
            {
                Vector3 flat = playerTarget.position - transform.position;
                flat.y = 0f;

                if (flat.sqrMagnitude > 0.0001f)
                {
                    Quaternion desired = Quaternion.LookRotation(flat.normalized);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        desired,
                        turnSpeed * Time.deltaTime);
                }
            }

            if (intents.Move && playerTarget != null)
            {
                Vector3 flat = playerTarget.position - transform.position;
                flat.y = 0f;

                if (flat.sqrMagnitude > 0.0001f)
                {
                    Vector3 step = flat.normalized * moveSpeed * Time.deltaTime;
                    transform.position += step;
                }
            }

            animator.SetFloat(SpeedHash, intents.Move ? 1f : 0f);

            if (intents.TriggerAttack)
            {
                _attackAnimPlaying = true;
                animator.SetTrigger(AttackHash);
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            _lastHitPoint = info.HitPoint;
            _attackAnimPlaying = false;

            _brain.OnDamaged();
            attackHitbox.CloseWindow();

            animator.SetTrigger(GetHitHash);
        }

        private void OnDied(DamageInfo info)
        {
            _brain.Kill();
            _attackAnimPlaying = false;

            attackHitbox.CloseWindow();
            animator.SetFloat(SpeedHash, 0f);
            animator.SetTrigger(DieHash);

            EventBus.Publish(new EnemyDiedEvent(enemyTypeId, transform.position, dropWeapon));

            enabled = false;
        }

        // ---------- 以下方法由你怪物动画上的 Animation Event 调用 ----------

        /// <summary>攻击动画出手帧。</summary>
        public void AttackStart()
        {
            if (_brain.State == EnemyState.Dead)
            {
                return;
            }

            attackHitbox.OpenWindow();
        }

        /// <summary>攻击动画收招帧。</summary>
        public void AttackEnd()
        {
            attackHitbox.CloseWindow();

            if (_attackAnimPlaying)
            {
                _attackAnimPlaying = false;
                _brain.OnAttackEnded();
            }
        }

        /// <summary>受击动画结束帧。</summary>
        public void HitEnd()
        {
            _brain.OnHitEnded();
        }
    }
}
```

**注意**：`AttackStart` / `AttackEnd` / `HitEnd` 这三个方法名**必须**与你怪物动画上的 Animation Event 名称一致。Unity 会把动画事件广播给该 GameObject 上所有含同名方法的组件——本项目主角对象上同时有 `PlayerController.AttackStart` 与 `PlayerCombat`（后者不含同名方法），因此不存在歧义。

- [ ] **Step 2: 编译检查**

运行编译命令。预期 `EXIT=0`。

- [ ] **Step 3: 手动验收**

**请用户执行：**

1. 打开 `Desert.unity`
2. 从 `Assets/RPGMonsterWave02PBR/Prefabs/Character/` 拖 `LizardWarriorPBRDefault` 到远离村庄处
3. 挂上你做的怪物 Animator Controller
4. 加 `CapsuleCollider`（`isTrigger` 不勾选）
5. 挂 `HealthComponent`（`maxHp = 60`、`displayName = 蜥蜴战士`、`isPlayer` 不勾选）
6. 挂 `EnemyAI`，填 `enemyTypeId = LizardWarrior`
7. 建子对象 `AttackHitbox`，位置放在怪物身前；挂 `Hitbox`（`flatDamage = 8`、`stats` 留空、`radius = 1.0`）
8. `EnemyAI` 的 `animator` / `attackHitbox` / `playerTarget`（拖主角）拖好
9. 进 Play，靠近怪物

**预期**：怪物转身朝你走来（不穿墙，但可能卡在石头上——这是 transform 追击的已知取舍）；进入近距离后播放攻击动画，玩家 HP 下降；打它时播放受击动画；打死时播放死亡动画且 HUD 出现 `死亡: LizardWarrior`。

- [ ] **Step 4: 快照**（`<N>` = `11`）

---

## Task 12: DropSpawner + DroppedItem

**Phase 07。**

**Files:**
- Create: `Assets/Script/Interaction/DroppedItem.cs`
- Create: `Assets/Script/Drop/DropSpawner.cs`

**Interfaces:**
- Consumes: `EventBus.Subscribe<EnemyDiedEvent>`、`ItemData`、`IInteractable`
- Produces:
  - `Demo.Interaction.DroppedItem`，`public void Setup(ItemData item, int amount)`
  - `Demo.Drop.DropSpawner`

- [ ] **Step 1: 实现 DroppedItem**

`Assets/Script/Interaction/DroppedItem.cs`（内容以 U+FEFF 开头）：

```csharp
using UnityEngine;
using Demo.Core;
using Demo.Data;

namespace Demo.Interaction
{
    /// <summary>
    /// 地面掉落物。只发布 ItemPickedUpEvent，不引用 InventorySystem。
    /// 由 DropSpawner 在运行时动态挂载，无需手工制作 prefab。
    /// </summary>
    public class DroppedItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData item;
        [SerializeField] private int amount = 1;

        public string PromptText
        {
            get { return "拾取"; }
        }

        public Transform Transform
        {
            get { return transform; }
        }

        public ItemData Item { get { return item; } }
        public int Amount { get { return amount; } }

        public void Setup(ItemData data, int count)
        {
            item = data;
            amount = count;
        }

        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled && item != null;
        }

        public void Interact(GameObject interactor)
        {
            if (item == null)
            {
                return;
            }

            EventBus.Publish(new ItemPickedUpEvent(item, amount));

            Destroy(gameObject);
        }
    }
}
```

- [ ] **Step 2: 实现 DropSpawner**

`Assets/Script/Drop/DropSpawner.cs`（内容以 U+FEFF 开头）：

```csharp
using UnityEngine;
using Demo.Core;
using Demo.Data;
using Demo.Interaction;

namespace Demo.Drop
{
    /// <summary>
    /// 订阅 EnemyDiedEvent 并生成掉落物。
    /// 怪物本身不知道本类的存在。
    /// 掉落物的碰撞体与 DroppedItem 组件全部在运行时添加，
    /// 不需要用户手工制作掉落物 prefab。
    /// </summary>
    public class DropSpawner : MonoBehaviour
    {
        [SerializeField] private float spawnHeight = 0.5f;
        [SerializeField] private float colliderPadding = 1.2f;

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        private void OnEnemyDied(EnemyDiedEvent e)
        {
            if (e.Drop == null || e.Drop.weaponPrefab == null)
            {
                return;
            }

            Spawn(e.Drop, e.Position);
        }

        private void Spawn(WeaponData weapon, Vector3 position)
        {
            GameObject instance = Instantiate(
                weapon.weaponPrefab,
                position + Vector3.up * spawnHeight,
                Quaternion.identity);

            instance.name = "Drop_" + weapon.displayName;

            // 剥掉模型自带的碰撞体，避免与交互查询互相干扰。
            Collider[] existing = instance.GetComponentsInChildren<Collider>();

            for (int i = 0; i < existing.Length; i++)
            {
                Destroy(existing[i]);
            }

            var dropped = instance.AddComponent<DroppedItem>();
            dropped.Setup(weapon, 1);

            // 补一个供交互查询命中的碰撞体。
            var box = instance.AddComponent<BoxCollider>();
            Bounds bounds = CalculateBounds(instance);

            box.center = instance.transform.InverseTransformPoint(bounds.center);
            box.size = bounds.size * colliderPadding;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}
```

- [ ] **Step 3: 编译检查**

运行编译命令。预期 `EXIT=0`。

- [ ] **Step 4: 手动验收**

**请用户执行：**

1. 打开 `Desert.unity`
2. 主角和怪物都加 `HealthComponent`
3. 建 `WeaponData` 资产：`id = LizardWarriorBlade`、`displayName = 蜥蜴战士之刃`、`damage = 7`、`weaponPrefab` 拖入 `Assets/RPGMonsterWave02PBR/Prefabs/Weapon/LizardWarriorBlade.prefab`
4. 怪物 `EnemyAI` 的 `dropWeapon` 拖入该资产
5. 建空对象 `GameSystems`（若已有则复用），挂 `DropSpawner`
6. 进 Play，打死怪物

**预期**：怪物死亡后原地出现武器模型；走近出现 `[E] 拾取`；按 E 后模型消失，HUD 事件区出现 `拾取: 蜥蜴战士之刃 x1`。

**说明**：Phase 08（背包）不在本切片内，因此拾取的物品不会进入任何容器，只发布事件。这是与 Phase 08 的明确接口。

- [ ] **Step 5: 快照**（`<N>` = `12`）

---

## Task 13: 端到端联调与验收

**Phase 07 收尾。**

**Files:**
- 无新增文件

- [ ] **Step 1: 全量编译检查**

运行 Global Constraints 第 12 条命令。预期 `EXIT=0`，`无编译错误`。

- [ ] **Step 2: 全量测试**

关闭编辑器，运行测试命令。预期 `EXIT=0`，全部用例通过：

| 测试类 | 用例数 | 来源 |
|---|---|---|
| `EventBusTests` | 6 | Task 1 |
| `InputLockTests` | 6 | Task 1 |
| `DataAssetTests` | 6 | Task 2 |
| `InteractionDetectionTests` | 3 | 执行期新增 |
| `DialogueSystemTests` | 11 | Task 4 |
| `QuestSystemTests` | 12 | Task 6 |
| `DamageCalculatorTests` | 4 | Task 8 |
| `EnemyBrainTests` | 16 | Task 10 |
| **合计** | **64** | |

---

## 执行记录（执行期对计划的修订）

执行过程中发现并就地修正的问题，按发生顺序记录。**代码以 `Assets/` 下的实际文件为准**，本节之上那些已被修订的代码块不再代表最终状态。

| # | 位置 | 问题 | 处理 |
|---|---|---|---|
| 1 | 约束 5 | 原文写 `Demo.Debug` 命名空间 | 改为 `Demo.Debugging`。`Demo.Debug` 会让 `Debug.Log` 解析到命名空间自身而非 `UnityEngine.Debug` |
| 2 | Task 3 | 漏了 `PlayerInteractionDetector` 的挂载说明，导致验收时提示不出现 | 接线清单改为按对象分组；补入排查顺序 |
| 3 | Task 3/5 | `NpcInteractable.SetRunner()` 无任何调用者（死代码）；`DialogueRunner.FindQuestComponent()` 用 `GetComponent` 强制三组件共置 | 全部改为 `[SerializeField]` 显式引用；去掉共置约束；`QuestComponent` 改挂主角 |
| 4 | Task 3 验收 | NPC prefab 路径正确但文件名写错（目录下是 `MC01`…`MC20`） | 更正 |
| 5 | Task 4 测试 | `TrySelectOption_*` 三个用例只推进一次 `TryAdvance`，而 fixture 有 2 句台词，选项根本没弹出 → 2 个失败 + 2 个**假通过** | 补 `AdvanceToOptions()` 辅助方法，内含前置断言，前提不成立即报错 |
| 6 | Task 4 测试 | `TrySelectOption_OnStartFrame_IsIgnored` 覆盖的守卫**不可达** | 移除该用例，改写为 `TrySelectOption_WhenNoOptionsPresented_ReturnsFalse`（覆盖真正生效的 `_awaitingOption` 守卫）；不可达的守卫保留但注释写明是防御性质 |
| 7 | Task 6 测试 | 新增 `Accept_WithNullQuest_ReturnsFalse` | 用例数 11 → 12 |
| 8 | 新增 | `InteractionDetectionTests` | 守护三个前提：接口类型可被 `GetComponentInParent` 解析、父对象上的组件可被找到、新建碰撞体可被 `OverlapSphere` 查到 |
| 9 | Task 9 | **`PlayerController.cs` 是 GBK 无 BOM**（`757369` 开头；UTF-8 解码在偏移 145 处因 `0xd2` 失败，GBK 解码成功） | **禁止对其使用 Edit/Write 工具**（二者写 UTF-8，会摧毁全文中文注释）。改用 Python 按 GBK 解码→改→编码回写，并做「反向替换后与原文逐字节相等」的往返校验。改动 18076→18637 字节、842→862 行（+20 = 10+5+5） |
| 10 | Task 8/9 | 编译验证 | 两个程序集均重编：`Csc Demo.Runtime.dll`、`Csc Demo.Tests.EditMode.dll`；`error CS` 零条；8 个 runtime 新文件与 `DamageCalculatorTests.cs` 均在 `.rsp` 编译单元内 |
| 11 | 流程 | batchmode Unity 在 `Batchmode quit successfully` 之后**退出阶段卡死**（本次 PID 30848 挂起 25 分钟以上），持续占用项目锁，后续测试无法启动 | 命令行检查步骤需自带超时与清理：轮询进程，超过阈值仍未退出则结束该无头进程（它已完成工作、无未保存状态）。**勿与用户的编辑器进程混淆** |

| 12 | Task 10 | 计划的 `EnemyBrain` 实现与它自己的测试矛盾：`TickChase` 置 `_attackTriggerPending = true` 后**直接**返回 `TriggerAttack = true`，该标志从未被消费；下一帧进入 `TickAttack` 又读到它仍为 `true`，于是**再次**返回 `true`。测试 `Chase_WithinAttackRange_EntersAttack_AndTriggersOnce` 断言第 3 次 Tick 的 `TriggerAttack` 为 `false`，必然失败 | 触发应只发生在 Chase→Attack 的**跃迁帧**。修复：`TickChase` 跃迁时直接返回 `true` 并不再置标志；`TickAttack` 恒返回 `TriggerAttack = false`；删除 `_attackTriggerPending` 字段及其在 `OnDamaged`/`Kill` 中的赋值。**游戏内后果不只是测试失败**：`EnemyAI` 会把每次 `TriggerAttack` 转成 `animator.SetTrigger(Attack)`，重复触发会导致攻击动画被反复重置。<br>**已按原文先行跑红灯实测**：`total=64 passed=63 failed=1`，唯一失败正是 `Chase_WithinAttackRange_EntersAttack_AndTriggersOnce`，报 `Expected: False / But was: True`——与上述走查完全一致，同时排除了该用例是**假通过**的可能。修复后绿灯 `total=64 passed=64 failed=0` |
| 13 | Task 11 | `EnemyAI` 在 `animator`/`attackHitbox` 为空时 `Update` **静默 return**，怪物站着不动且无任何提示；同时 `OnDamaged`/`OnDied` 无条件调用这两者，漏挂即 NRE | 加一次性告警 `WarnMissingRefsOnce()`（只报一次，不刷屏，与 `DialogueRunner.verboseLog` 同一思路）；`OnDamaged`/`OnDied`/`AttackStart`/`AttackEnd` 内的调用加空引用保护。**纯加法，行为不变** |
| 14 | 流程 | 退出卡死会**持有项目锁**：后续 Unity 运行直接 exit 21（"似乎有另一个正在运行的 Unity 实例打开了此项目"）并触发崩溃处理器。等待自行退出耗时可达 25 分钟以上 | 命令行检查的调用需用 `timeout -k 10 420` 之类包住**自己启动的**进程；若仍被上一轮僵尸挡住，只能手动清理。**僵尸进程只应清理由本轮启动的，勿误伤用户编辑器** |

**GBK 文件的判据**（适用于本项目中任何既有脚本）：

```bash
python -c "
b=open('路径','rb').read()
print('BOM' if b[:3]==b'\xef\xbb\xbf' else 'no-BOM')
try: b.decode('utf-8'); print('utf-8 OK')
except Exception as e: print('utf-8 FAIL ->', e)
"
```

`no-BOM` + `utf-8 FAIL` + GBK 可解码 ⇒ 该文件必须走 Python 字节级编辑。新增文件仍一律写 UTF-8 + U+FEFF。

- [ ] **Step 3: 检查 Unity 控制台**

**请用户执行**：打开 Unity，进 Play，确认 Console **无红色报错**、无 `NullReferenceException`。

- [ ] **Step 4: 端到端手动验收**

**请用户按顺序执行完整流程：**

1. 从出生点出发，找到 NPC
2. 走近 → 出现 `[E] 对话` → 按 E
3. 对话逐句推进 → 选择"我接受"
4. HUD 任务区显示 `讨伐魔物：0/3 （进行中）`
5. 前往怪物区
6. 攻击怪物 → 怪物 HP 下降、播放受击动画
7. 怪物反击 → 玩家 HP 下降
8. 击杀 3 只 → HUD 依次显示 `1/3`、`2/3`、`3/3`
9. `3/3` 时 HUD 显示 `讨伐魔物：3/3  ✔ 已完成`
10. 地上出现武器掉落物
11. 走近 → `[E] 拾取` → 按 E → 模型消失，HUD 记录拾取

- [ ] **Step 5: 记录验收结果**

把"通过 / 未通过 + 现象描述"写回本文件末尾的"验收记录"小节。

- [ ] **Step 6: 快照**（`<N>` = `13`）

---

## 验收记录

> 由用户在 Task 13 完成后填写。

| 检查项 | 结果 | 备注 |
|---|---|---|
| 全量编译无错误 | ✅ 通过（2026-09-13，自动） | `Csc Demo.Runtime.dll` / `Csc Demo.Tests.EditMode.dll` 均重编，`error CS` **0 条** |
| 64 个 EditMode 用例全通过 | ✅ 通过（2026-09-13，自动） | `total=64 passed=64 failed=0`；各套件用例数与计划表格逐项吻合 |
| Console 无红色报错 | | |
| 对话逐句推进正常 | | |
| 同帧 E 键不跳过第一句 | | |
| 接任务后 HUD 显示 0/3 | | |
| 同一次挥砍只结算一次伤害 | | |
| 击杀 3 只后显示 3/3 已完成 | | |
| 怪物掉落武器 | | |
| 按 E 可拾取 | | |

---

## 附：已知取舍与风险

| 项 | 说明 |
|---|---|
| 怪物移动不避障 | transform 直接转向追击，可能卡在石头/建筑上。这是决策 4 的已知代价 |
| 怪物之间可互相伤害 | `Hitbox.targetLayers` 默认 `~0`。若需隔离，收窄 LayerMask |
| 对话期间仍可移动/翻滚/跳跃 | `InputLock` 只屏蔽攻击与交互。若需完全冻结，需给 `PlayerController` 加输入屏蔽（超出本切片范围） |
| 拾取物无去处 | Phase 08 前只发布事件 |
| ~~`DialogueRunner` 与 `QuestComponent` 必须同对象~~ | **已消除**（见执行记录 #3）：改为 `[SerializeField]` 显式引用，不再要求三组件共置。`QuestComponent` 现挂在 `GameSystems` 上 |
| 攻击判定依赖动画事件 | 若 FBX 内嵌剪辑无法打动画事件，改用计时器开合窗口（见 spec §14 风险表） |
