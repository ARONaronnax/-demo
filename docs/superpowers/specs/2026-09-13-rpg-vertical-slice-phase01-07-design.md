# RPG Demo Vertical Slice —— Phase 01–07 设计

日期：2026-09-13
状态：待评审
范围：任务书 Phase 01–07（架构 / Interaction / Dialogue / Quest / Combat / Enemy AI / Drop）

---

## 1. 背景

### 1.1 项目现状（已核实，非推测）

Unity **2022.3.62f3c1**，Built-in 渲染管线。已装包：uGUI 1.0.0、TextMeshPro 3.0.7、Cinemachine 2.10.7、Timeline、Visual Scripting、`com.unity.modules.ai`、`com.unity.test-framework` 1.1.33。使用**旧版 Input Manager**（未装新 Input System），与现有代码的 `Input.GetAxisRaw` 一致。

**现有代码只有 2 个文件：**

| 文件 | 行数 | 内容 |
|---|---|---|
| `Assets/Script/PlayerController.cs` | 841 | WASD + 相机相对移动、左键攻击、右键举盾、C 翻滚、Space 跳跃、H 受击、K 死亡 |
| `Assets/Script/ThirdPersonCamera.cs` | 50 | 鼠标环绕第三人称相机 |

两个文件均为 **GBK 编码 + CRLF**。

**`PlayerController` 对外契约（新系统必须满足）：**

- Animator 参数：`Speed`(float)、`IsDefend`/`IsRoll`(bool)、`Attack`/`Jump`/`GetHit`/`Die`(trigger)
- 动画事件：`AttackStart`、`AttackEnd`、`JumpStart`、`JumpEnd`、`HitEnd`
- 公开 API：`GetHit()`、`Die()`，属性 `IsDead` / `IsRolling` / `IsAttacking` / `IsJumping` / `IsHit` / `IsGrounded`

**工作场景：`Assets/Lowpoly Style/Desert/DemoScene/Desert.unity`**

- 1.1 MB，82 个 GameObject，含 `JOSHUA CAMP`、`DEAD CAMP`、`WESTERN VILLAGE`、`OASIS VILLAGE`、`DUNES`、`CACTUS`
- 全场景只有 3 个 MonoBehaviour：`PlayerController`（在主角对象 `fileID 439879436` 上）、`ThirdPersonCamera`（在 `Camera` 上）、`CinemachineBrain`（在 `Camera` 上，包自带）
- 主角对象同时还挂着 `CharacterController`；**`animator` 字段为空（`fileID: 0`）**

**可用素材：**

| 用途 | 位置 | 数量 |
|---|---|---|
| NPC | `Assets/RPGTinyHeroWavePBR/Prefab/npcCharacters/` | MC01–MC48 |
| 主角武器 | `Assets/RPGTinyHeroWavePBR/Prefab/Weapons/` | 30+ |
| 怪物 | `Assets/RPGMonsterWave02PBR/Prefabs/Character/` | 10 种 ×2 |
| 掉落武器 | `Assets/RPGMonsterWave02PBR/Prefabs/Weapon/` | 6：`BlackKnightAxe`、`BlackKnightShield`、`LizardWarriorBlade`、`LizardWarriorShield`、`RatAssasinDagger`、`SpecterBlade` |
| 怪物动画 | 各怪物 `Animations/<名字>/` | IdleNormal、IdleBattle、Run、WalkFWD、Attack01–03、GetHit、Die |

### 1.2 与任务书前提的差异

任务书称项目"已经具备完整地图场景、村庄环境、篝火"。实际情况：

- **地图场景存在且质量良好**（Desert，含两个村庄、两个营地）—— 但这是资源包自带的场景，任务相关内容（NPC、怪物、任务点、掉落）**一个都没有**
- **主角已在场景中并挂好 `PlayerController` + `CharacterController`**，但 Animator 未接线，因此当前进 Play 会每帧抛 `NullReferenceException`（`PlayerController.Update` → `animator.SetFloat`）
- 主角与怪物的 **Animator Controller（状态机）由你负责实现**，本设计只定义契约

### 1.3 上一轮工作的处置

`docs/superpowers/` 下存在 2026-09-12 的设计文档与 4400 行 / 16 Task 的计划，内容是对主角系统做重构（引入 asmdef、`HeroAnimatorBuilder` 生成器、`PlayerMotor` / `PlayerAnimatorDriver` / `PlayerCameraRig` 分层、`DemoSceneBuilder`）。

**该计划一行未落地**，当前 `PlayerController.cs` 是更简单的自包含版本（2026-09-13 修改）。

**处置：不复活该重构。** 本设计在现有 `PlayerController` 之上增量开发，只对它做一处 4 行的加法改动（见 §8.4）。

---

## 2. 目标与非目标

### 2.1 目标

跑通一条自洽的纵向切片：

> 篝火出生 → 找到 NPC → 按 E 对话 → 接受"讨伐魔物"任务 → 前往怪物区 → 用现有攻击动画击杀 3 只 → 任务进度 0/3→3/3 → 任务完成 → 怪物掉落武器 → 按 E 拾取

### 2.2 非目标（本切片不做）

- 背包系统（Phase 08）、装备系统（Phase 09）
- 正式 UI：QuestUI / DialogueUI / InventoryUI / WeaponDetailUI（Phase 10–11）
- 存档、音效、多任务并行、任务奖励发放
- 修改 `PlayerController` 的移动 / 翻滚 / 跳跃 / 举盾逻辑
- 生成或修改任何 Animator Controller（由你实现）

### 2.3 本切片 UI 的替代方案

任务书的 Quest 验收标准是"Quest UI 显示 0/3"，Interaction 是"看到 E 提示"，但正式 UI 排在 Phase 10–11。为避免"做完了却无法用眼睛验收"，本切片期间提供一个**临时调试 HUD**（IMGUI，见 §11），Phase 10–11 时整体删除。

---

## 3. 已确认的决策

| # | 决策 | 选定 |
|---|---|---|
| 1 | Unity 资产（状态机 / 场景 / prefab）由谁制作 | **你**（本设计只定义契约与接线清单，不生成任何资产） |
| 2 | 验证方式 | **我**用命令行 Unity 做编译检查 + EditMode 测试；**你**做手动试玩验收 |
| 3 | 切片终点 | **Phase 01–07** |
| 4 | 怪物移动 | **transform 直接转向追击**，不使用 NavMesh |
| 5 | 怪物 Animator 参数命名 | 与主角侧一致：`Speed` / `Attack` / `GetHit` / `Die` |
| 6 | 玩家识别方式 | **不使用 Tag，不使用 Layer**（见 §8.5.3） |
| 7 | 通信机制 | 静态类型安全 `EventBus`（非 ScriptableObject 事件通道） |
| 8 | 逻辑分层 | 纯逻辑 C# 类 + MonoBehaviour 桥接（见 §5.2） |

---

## 4. 环境约束（对实现有强制影响）

| 约束 | 影响 |
|---|---|
| 旧版 Input Manager | 交互键用 `Input.GetKeyDown(KeyCode.E)`，与现有风格一致 |
| 项目不是 git 仓库（有 `.gitignore`，无 `.git`） | 本文档写盘后**未提交**，无版本历史兜底 |
| 现有脚本为 GBK 编码 | 新建文件统一 **UTF-8 with BOM**（VS 与 Unity 均可正确识别中文）；不批量转码现有文件 |
| 场景无 NavMesh | 与决策 4 一致 |
| 场景已有 `CinemachineBrain` 但无虚拟相机 | 当前无冲突。**注意：一旦往场景里添加 Cinemachine 虚拟相机，会与 `ThirdPersonCamera` 争夺相机控制权** |
| 主角对象 `animator` 字段为空 | 在你完成状态机接线前，进 Play 会持续抛 `NullReferenceException`。本切片所有验收都以"状态机已接线"为前提 |

---

## 5. 架构

### 5.1 目录与程序集

```
Assets/Script/
├── Demo.Runtime.asmdef
├── Core/           EventBus, GameEvents, InputLock, IDamageable, DamageInfo
├── Data/           ItemData, WeaponData, ConsumableData, QuestItemData,
│                   QuestData, DialogueData            ← ScriptableObject
├── Combat/         HealthComponent, Hitbox, DamageCalculator
├── Player/         PlayerCombat, PlayerStats, PlayerInteractionDetector
├── Enemy/          EnemyAI (MonoBehaviour), EnemyBrain (纯逻辑), EnemyState
├── Interaction/    IInteractable, NpcInteractable, DroppedItem
├── Quest/          QuestSystem (纯逻辑), QuestStatus, QuestComponent
├── Dialogue/       DialogueSystem (纯逻辑), DialogueRunner
├── Drop/           DropSpawner
└── Debug/          DebugHud                          ← Phase 10–11 时删除

Assets/Tests/EditMode/
├── Demo.Tests.EditMode.asmdef
├── EventBusTests.cs
├── QuestSystemTests.cs
├── DialogueSystemTests.cs
├── EnemyBrainTests.cs
├── DamageCalculatorTests.cs
└── DataAssetTests.cs
```

**为什么必须引入 asmdef：** Unity 的测试程序集无法引用 `Assembly-CSharp`。项目当前没有任何 asmdef，因此**不建 asmdef 就写不了 EditMode 测试**，决策 2 无法成立。

**asmdef 配置：**

- `Demo.Runtime.asmdef`：`autoReferenced: true`（默认）、`noEngineReferences: false`（默认）。`Assembly-CSharp` 会自动引用它，因此 `Assets/Lowpoly Style/Shared Scripts/`（`FlickerLight.cs`、`UVOffset.cs`）不受影响。
- `Demo.Tests.EditMode.asmdef`：`includePlatforms: ["Editor"]`，引用 `Demo.Runtime` + `UnityEngine.TestRunner` + `UnityEditor.TestRunner`，`precompiledReferences: ["nunit.framework.dll"]`，`defineConstraints: ["UNITY_INCLUDE_TESTS"]`。

### 5.2 分层原则

**纯逻辑层**（普通 C# 类，不继承 MonoBehaviour，不引用场景）：

`QuestSystem`、`DialogueSystem`、`EnemyBrain`、`DamageCalculator`

**桥接层**（MonoBehaviour，只做两件事：持有 Inspector 引用、转发事件）：

`QuestComponent`、`DialogueRunner`、`EnemyAI`、`PlayerCombat`、`Hitbox`、`HealthComponent`、`DropSpawner`、`PlayerInteractionDetector`

**为什么这样分：** 决策 2 要求我做 EditMode 测试。纯逻辑类可以直接 `new` 出来测，不需要进 PlayMode、不需要加载场景，测试快且不 flaky。若把这些逻辑写进 MonoBehaviour，每个测试都要起场景，慢且容易假失败。

**约束：** 纯逻辑层禁止出现 `UnityEngine` 的场景类型（`GameObject`/`Transform`/`MonoBehaviour`）。允许使用 `Vector3`、`Mathf` 等纯数学类型（`EnemyBrain` 需要 `Vector3` 做距离判定）。

### 5.3 通信机制

`Core/EventBus.cs` —— 静态、类型安全的发布订阅：

```csharp
public static class EventBus
{
    public static void Subscribe<T>(Action<T> handler) where T : struct;
    public static void Unsubscribe<T>(Action<T> handler) where T : struct;
    public static void Publish<T>(T evt) where T : struct;
    public static void Clear();
}
```

设计要点：

- **`where T : struct`** —— 事件为值类型，杜绝 null 事件对象，"一个事件 = 一个结构体"语义清晰
- **`Publish` 先取委托快照再逐个调用** —— 防止处理器在回调中订阅/退订导致"集合被修改"异常
- **`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 中调用 `Clear()`** —— 关闭 Domain Reload 时静态状态会跨 Play 残留，这是必需的防护
- **所有订阅严格成对：`OnEnable` 订阅 / `OnDisable` 退订**，无例外

**为什么不选 ScriptableObject 事件通道：** 该方案在 Inspector 上可视化、资产化，但调试成本高（断点难打、订阅关系散落在资产里），在这个规模上没有收益。若你偏好该方案，改动集中在 `EventBus.cs` 一个文件。

### 5.4 输入锁

`Core/InputLock.cs` —— 对话期间需要屏蔽攻击/翻滚/交互输入：

```csharp
public static class InputLock
{
    public static bool IsLocked { get; }
    public static void Acquire(object owner);
    public static void Release(object owner);
    public static void Clear();
}
```

用 `HashSet<object>` 按持有者记录，避免"重复 Release 导致锁提前释放"。同样在 `SubsystemRegistration` 时 `Clear()`。

**接入方式：** `DialogueRunner` 打开时 `Acquire(this)`，关闭时 `Release(this)`。`PlayerCombat`、`PlayerInteractionDetector` 在 `IsLocked` 时直接 return。**`PlayerController` 不做修改**——本切片不要求对话期间禁止移动（翻滚/跳跃仍可用，属已知的可接受瑕疵）。

---

## 6. 事件契约

`Core/GameEvents.cs` 中定义以下结构体：

| 事件 | 字段 | 发布者 | 订阅者 |
|---|---|---|---|
| `InteractionPromptChangedEvent` | `string PromptText`、`bool Visible` | `PlayerInteractionDetector` | `DebugHud` |
| `InteractedEvent` | `string TargetName` | `PlayerInteractionDetector` | `DebugHud` |
| `DialogueStartedEvent` | `DialogueData Data` | `DialogueRunner` | `DebugHud` |
| `DialogueLineChangedEvent` | `string Speaker`、`string Line`、`int Index`、`int Total` | `DialogueRunner` | `DebugHud` |
| `DialogueEndedEvent` | `DialogueData Data` | `DialogueRunner` | `DebugHud` |
| `QuestAcceptedEvent` | `QuestData Quest` | `QuestComponent` | `DebugHud` |
| `QuestProgressChangedEvent` | `QuestData Quest`、`int Current`、`int Required` | `QuestComponent` | `DebugHud` |
| `QuestCompletedEvent` | `QuestData Quest` | `QuestComponent` | `DebugHud` |
| `EntityDamagedEvent` | `string TargetName`、`float Amount`、`float RemainingHp`、`bool IsPlayer` | `HealthComponent` | `DebugHud` |
| `EnemyDiedEvent` | `string EnemyTypeId`、`Vector3 Position`、`WeaponData Drop` | `EnemyAI` | `QuestComponent`、`DropSpawner`、`DebugHud` |
| `ItemPickedUpEvent` | `ItemData Item`、`int Amount` | `DroppedItem` | `DebugHud`（Phase 08 起由 `InventorySystem` 接管） |

**关键解耦（任务书第 7 条要求）：**

- `Enemy` **不引用** `QuestSystem`。`EnemyAI` 只发布 `EnemyDiedEvent`；`QuestComponent` 订阅该事件并转成任务进度。
- `Enemy` **不引用** `DropSystem`。`EnemyAI` 只发布 `EnemyDiedEvent`（携带 `Drop`）；`DropSpawner` 订阅该事件并生成掉落物。
- `DroppedItem` **不引用** `InventorySystem`。只发布 `ItemPickedUpEvent`。
- `DialogueSystem` **不引用** `QuestSystem`。`DialogueRunner` 在对话结束时检查选中的选项是否携带 `questToOffer`，若有则调用 `QuestComponent.Accept()`。

---

## 7. 数据层（ScriptableObject）

### 7.1 物品

```csharp
public enum ItemType { Weapon, Consumable, Quest }

public abstract class ItemData : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    public int maxStack = 1;
    public ItemType itemType;
}

public class WeaponData : ItemData
{
    public int damage;
    public GameObject weaponPrefab;        // 手持模型 & 掉落物模型共用
    public Vector3 socketLocalPosition;
    public Vector3 socketLocalEuler;
    public Vector3 socketLocalScale = Vector3.one;
}

public class ConsumableData : ItemData { public int healAmount; }
public class QuestItemData : ItemData { }
```

**关于继承：** Unity 中 ScriptableObject 的子类资产可直接赋给基类字段（`public ItemData item;` 拖入 `WeaponData` 资产是合法的），多态引用天然可用，不需要 `[SerializeReference]`。

`weaponPrefab` 同时用于手持模型（Phase 09）与掉落物模型（Phase 07），避免维护两套 prefab。

### 7.2 任务

```csharp
public class QuestData : ScriptableObject
{
    public string id;                   // "KillMonsters"
    public string title;                // "讨伐魔物"
    [TextArea] public string description;
    [TextArea] public string objectiveText;   // "击败 3 只魔物"
    public int requiredAmount = 3;
    public string targetEnemyTypeId;    // 留空 = 任意敌人都计数
    public ItemData rewardItem;         // 本切片不发放，Phase 08 起使用
    public int rewardAmount;
}
```

`targetEnemyTypeId` 留空即"任意怪物计数"，符合任务书"击败 3 只魔物"。同时保留了"只计特定怪"的能力，且该分支可被测试覆盖。

### 7.3 对话

```csharp
[System.Serializable]
public class DialogueOption
{
    public string text;
    public DialogueData nextDialogue;   // 留空 = 结束对话
    public QuestData questToOffer;      // 非空 = 选此项即接受该任务
}

public class DialogueData : ScriptableObject
{
    public string speakerName;
    [TextArea] public string[] lines;
    public DialogueOption[] options;    // 在最后一句之后显示；留空 = 说完即结束
}
```

**任务状态分支的处理：** `DialogueData` 保持纯粹，不承载"已接任务/已完成"的分支。由 `NpcInteractable` 持有三个 `DialogueData` 引用（`dialogueBeforeQuest` / `dialogueQuestActive` / `dialogueQuestCompleted`），在交互时查询 `QuestSystem` 状态选一个播放。这样对话数据本身与环境无关，可复用。

---

## 8. 系统设计

### 8.1 Phase 02 —— Interaction

```csharp
public interface IInteractable
{
    string PromptText { get; }              // 返回动词："对话" / "拾取"
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
    Transform Transform { get; }
}
```

```csharp
public class PlayerInteractionDetector : MonoBehaviour
{
    [SerializeField] float radius = 2.5f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    // 每帧 OverlapSphereNonAlloc → 取最近的有效 IInteractable
    // 目标变化时发布 InteractionPromptChangedEvent("[E] " + PromptText)
    // 按 E 时调用 Interact(gameObject)
}
```

**不使用 Layer / Tag 的关键决策：** 检测用 `Physics.OverlapSphereNonAlloc` 拿到所有碰撞体后，用 `GetComponentInParent<IInteractable>()` 过滤。这样**不需要你新建 Layer、不需要改 LayerMask 配置**，代价是每帧几次 `GetComponent`（半径 2.5m 内，开销可忽略）。LayerMask 可作为后续优化项加入，不阻塞本切片。

**`NpcInteractable`** —— 挂在你摆的 NPC 上，实现 `IInteractable`：`PromptText => "对话"`。

`Interact()` 的行为**分两个 Phase 递进**，因为 `DialogueRunner` 到 Phase 03 才存在：

- **Phase 02**：`Interact()` 发布 `InteractedEvent` 并结束。`DialogueRunner` 字段此时为空，**必须做 null 判断**，不能直接调用。
- **Phase 03**：接入 `DialogueRunner` 后，`Interact()` 先查询任务状态选出对应的 `DialogueData`，再调用 `dialogueRunner.Begin(选中项)`；若 `dialogueRunner` 为空则退回 Phase 02 的行为。

这样每个 Phase 结束时项目都处于可运行、可验收的状态，不会出现"Phase 02 做完一点就报空引用"。

**NPC 需要一个碰撞体**才能被 `OverlapSphere` 命中。NPC prefab（MC01–MC48）上可能没有，需要你补一个（见 §10）。

**交互屏蔽条件：** `InputLock.IsLocked`（对话中）、或玩家 `PlayerController.IsDead` 时，不检测、不响应。

### 8.2 Phase 03 —— Dialogue

```csharp
public class DialogueSystem          // 纯逻辑，无 Unity 场景依赖
{
    public bool IsRunning { get; }
    public string SpeakerName { get; }
    public string CurrentLine { get; }
    public bool HasOptions { get; }
    public IReadOnlyList<DialogueOption> CurrentOptions { get; }

    public void Start(DialogueData data);

    // 返回 false = 本次输入被同帧守卫拦下（见下方"同帧输入冲突"）
    public bool TryAdvance(int currentFrame);              // 下一句；已是最后一句则进入选项/结束
    public bool TrySelectOption(int index, int currentFrame);

    public event Action<DialogueData> Started;
    public event Action<string, int, int> LineChanged;      // line, index, total
    public event Action<IReadOnlyList<DialogueOption>> OptionsPresented;
    public event Action<DialogueOption> OptionSelected;
    public event Action<DialogueData> Ended;
}
```

**状态推进规则：**

1. `Start(data)` → 显示 `lines[0]`，`IsRunning = true`，记录 `StartFrame = currentFrame`
2. `TryAdvance(f)` → index+1；若越界：
   - `options` 非空 → 触发 `OptionsPresented`，等待 `TrySelectOption`
   - `options` 为空 → 触发 `Ended`，`IsRunning = false`
3. `TrySelectOption(i, f)` → 触发 `OptionSelected`；若该选项 `nextDialogue` 非空则 `Start(nextDialogue)`，否则 `Ended`

**为什么把帧号作为参数传入，而不是在类内读 `Time.frameCount`：** `DialogueSystem` 是纯逻辑类，读 `Time.frameCount` 会引入对 Unity 运行时的隐式依赖，EditMode 测试里无法控制。把帧号作为参数注入后，同帧守卫可以在 EditMode 里直接测试。这是本设计里唯一一处为可测性而调整的 API 形状。

**`DialogueRunner`**（MonoBehaviour 桥接）：持有 `DialogueSystem`，`Begin(data)` 时 `InputLock.Acquire(this)` 并把状态转成 `EventBus` 事件；`Ended` 时 `InputLock.Release(this)`，并检查最后选中的选项是否携带 `questToOffer` → 调用 `QuestComponent.Accept(quest)`。

**本切片的对话输入：** 用键盘推进（`E` 或 `Space` 下一句，数字键 `1`/`2`/`3` 选选项），因为还没有 DialogueUI 可点。调试 HUD 负责显示。Phase 11 接上正式 UI 后改为鼠标点击。

**必须处理的同帧输入冲突：** 玩家按 `E` 触发交互，同一个 `E` 按键在同一帧又会推进对话，导致对话刚开始就跳过第一句。守卫规则：`TryAdvance(f)` / `TrySelectOption(i, f)` 在 `f == StartFrame` 时直接返回 `false`，不改变任何状态。`DialogueRunner` 每帧把 `Time.frameCount` 传进去即可。

### 8.3 Phase 04 —— Quest

```csharp
public enum QuestStatus { NotStarted, InProgress, Completed }

public class QuestSystem             // 纯逻辑
{
    public QuestStatus GetStatus(QuestData q);
    public int GetProgress(QuestData q);
    public bool IsActive(QuestData q);

    public bool Accept(QuestData q);              // 已接受/已完成则返回 false
    public bool ReportKill(string enemyTypeId);   // 返回进度是否变化

    public event Action<QuestData> Accepted;
    public event Action<QuestData, int, int> ProgressChanged;   // quest, current, required
    public event Action<QuestData> Completed;
}
```

**`ReportKill` 规则：**

- 该任务未处于 `InProgress` → 不计数，返回 `false`
- `targetEnemyTypeId` 非空且与 `enemyTypeId` 不匹配 → 不计数，返回 `false`
- 否则 `progress++`，触发 `ProgressChanged`
- `progress >= requiredAmount` 时转为 `Completed`，触发 `Completed`
- **已完成后再收到击杀事件不再计数**（防止重复完成）

**`QuestComponent`**（MonoBehaviour 桥接）：持有 `QuestSystem`，`OnEnable` 订阅 `EventBus.Subscribe<EnemyDiedEvent>` → `ReportKill(evt.EnemyTypeId)`，并把 `QuestSystem` 的 C# 事件转发成 `EventBus` 事件给 HUD。

任务书要求的 `OnQuestAccepted` / `OnQuestUpdated` / `OnQuestCompleted` 三个事件，对应上表的 `Accepted` / `ProgressChanged` / `Completed`。

### 8.4 Phase 05 —— Combat

```csharp
public readonly struct DamageInfo
{
    public readonly float Amount;
    public readonly GameObject Attacker;
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitDirection;
}

public interface IDamageable
{
    bool IsAlive { get; }
    Transform Transform { get; }
    void TakeDamage(in DamageInfo info);
}
```

```csharp
public class HealthComponent : MonoBehaviour, IDamageable
{
    [SerializeField] float maxHp = 100f;
    [SerializeField] bool isPlayer = false;
    [SerializeField] string displayName = "Enemy";

    public float CurrentHp { get; }
    public float MaxHp { get; }
    public bool IsAlive => CurrentHp > 0f;

    public event Action<DamageInfo> Damaged;    // 受击（未死）
    public event Action<DamageInfo> Died;       // 死亡（仅触发一次）

    public void TakeDamage(in DamageInfo info);
    public void ResetHealth();
}
```

`TakeDamage` 在 `CurrentHp <= 0` 或已死亡时直接 return；扣血后发布 `EntityDamagedEvent`，`CurrentHp <= 0` 时置零并触发 `Died`（**只触发一次**）。`HealthComponent` 不认识任务、不认识掉落物。

```csharp
public class Hitbox : MonoBehaviour
{
    [SerializeField] PlayerStats stats;       // 非空 → 用 stats.FinalDamage
    [SerializeField] float flatDamage = 10f;  // stats 为空 → 用此值
    [SerializeField] LayerMask targetLayers = ~0;
    [SerializeField] float radius = 0.8f;
    [SerializeField] Vector3 localOffset = Vector3.zero;

    public void OpenWindow();    // 清空命中记录，开始逐帧检测
    public void CloseWindow();   // 停止检测
}
```

**关键设计 —— "一次攻击对同一目标只造成一次伤害"：**

`Hitbox` 内部持有 `HashSet<IDamageable> _hitThisWindow`，在 `OpenWindow()` 时清空。窗口打开期间每帧执行 `Physics.OverlapSphereNonAlloc`，命中且不在集合中的目标结算伤害并加入集合。

**为什么用重叠查询而不是 `OnTriggerEnter`：** 触发回调要求碰撞双方至少有一个 Rigidbody，且需要正确配置 Collider 的 `isTrigger`、Layer 碰撞矩阵。任一处配错就是"打了不掉血"，而且静默失败、极难排查。显式重叠查询**零场景配置**，行为确定，且逻辑上更容易测试。

`targetLayers` 默认 `~0`（Everything），配合"排除持有者自身"过滤。**已知瑕疵：** 默认配置下怪物之间可以互相打到。若你介意，把 `targetLayers` 收窄即可，不阻塞本切片。

**对 `PlayerController.cs` 的唯一改动（4 行，纯加法）：**

```csharp
public event Action AttackWindowOpened;
public event Action AttackWindowClosed;
```

在已有的 `AttackStart()` / `AttackEnd()` 方法体内各加一行 `AttackWindowOpened?.Invoke();` / `AttackWindowClosed?.Invoke();`。

**理由：** `AttackStart` / `AttackEnd` 是 `PlayerController` 上已有的动画事件方法。`PlayerCombat` 需要知道攻击窗口何时开合，但不能去改 `PlayerController` 的职责。加两个事件让 `PlayerController` 只声明"我的攻击窗口开了"，由 `PlayerCombat` 订阅并操作 `Hitbox`。这既保持了"`PlayerController` 不负责战斗"（任务书第 16 条），又避免依赖"动画事件会广播到同 GameObject 上所有组件"这种隐式行为。行为零变化。

```csharp
public class PlayerStats : MonoBehaviour
{
    [SerializeField] int baseDamage = 10;
    public WeaponData EquippedWeapon { get; private set; }
    public int FinalDamage => baseDamage + (EquippedWeapon != null ? EquippedWeapon.damage : 0);
    public event Action StatsChanged;
    public void SetWeapon(WeaponData weapon);   // Phase 09 的接入点
}
```

```csharp
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] PlayerController controller;
    [SerializeField] PlayerStats stats;
    [SerializeField] Hitbox hitbox;
    // OnEnable: controller.AttackWindowOpened += hitbox.OpenWindow
    //           controller.AttackWindowClosed += hitbox.CloseWindow
}
```

**伤害计算：** `DamageCalculator.Calculate(PlayerStats) => stats.FinalDamage`。本切片 `EquippedWeapon` 恒为 null，因此 `FinalDamage == baseDamage`；Phase 09 接装备后自动生效。任务书第 13 条要求的"战斗系统使用 FinalDamage，不要让 InventoryUI 自己算"由此满足。

### 8.5 Phase 06 —— Enemy AI

#### 8.5.1 状态机（纯逻辑，可测试）

```csharp
public enum EnemyState { Idle, Chase, Attack, Hit, Dead }

public class EnemyBrain
{
    public EnemyState State { get; }

    public void Tick(float deltaTime, in EnemySensors sensors);
    public void OnDamaged();      // 外部事件
    public void OnAttackEnded();  // 动画事件 AttackEnd
    public void OnHitEnded();     // 动画事件 HitEnd
}

public readonly struct EnemySensors
{
    public readonly bool HasTarget;
    public readonly float DistanceToTarget;
    public readonly bool HasLineOfSight;
}

public readonly struct EnemyIntents      // Tick 的输出
{
    public readonly bool ShouldMove;
    public readonly bool ShouldTriggerAttack;
    public readonly bool ShouldFaceTarget;
}
```

**转换规则：**

| 当前 | 条件 | 目标 |
|---|---|---|
| Idle | `HasTarget && Distance <= detectRange && (LineOfSight 或未启用视线要求)` | Chase |
| Chase | `Distance <= attackRange` | Attack |
| Chase | `!HasTarget` 或 `Distance > leashRange` | Idle |
| Attack | `OnAttackEnded()` | Chase |
| 任意（非 Dead） | `OnDamaged()` | Hit |
| Hit | `OnHitEnded()` | Chase（若有目标）否则 Idle |
| 任意 | HP ≤ 0 | Dead（**终态，不可回退**） |

`Dead` 为终态：`Tick` 直接返回零意图，`OnDamaged` / `OnAttackEnded` / `OnHitEnded` 一律忽略。

#### 8.5.2 MonoBehaviour 桥接

```csharp
public class EnemyAI : MonoBehaviour
{
    [SerializeField] HealthComponent health;
    [SerializeField] Animator animator;
    [SerializeField] Transform playerTarget;      // 留空则 Start 时按 Tag "Player" 找一次
    [SerializeField] float moveSpeed = 2f;
    [SerializeField] float turnSpeed = 8f;
    [SerializeField] float detectRange = 12f;
    [SerializeField] float attackRange = 2f;
    [SerializeField] float leashRange = 25f;
    [SerializeField] string enemyTypeId = "Werewolf";
    [SerializeField] WeaponData dropWeapon;
    [SerializeField] Hitbox attackHitbox;

    // 每帧：采集 sensors → brain.Tick → 应用 intents
    // 移动用 transform.position + Vector3.MoveTowards（决策 4：不用 NavMesh）
}
```

**死亡处理流程**（对应任务书第 9 条）：

1. `health.Died` 事件到达 → `brain` 置 `Dead`
2. 停止 AI：不再移动、不再触发攻击、关闭 `attackHitbox`
3. `animator.SetTrigger("Die")`
4. `EventBus.Publish(new EnemyDiedEvent(enemyTypeId, transform.position, dropWeapon))`
5. `QuestComponent` 与 `DropSpawner` 各自订阅并响应

#### 8.5.3 玩家识别（决策 6）

**不使用 Tag，不使用 Layer。** `EnemyAI.playerTarget` 是一个 `[SerializeField] Transform`，你在 Inspector 里把主角对象拖进去。每个怪物拖一次。

若该字段为空，`Start()` 中回退到 `GameObject.FindGameObjectWithTag("Player")` **一次**（不是每帧）。这只是兜底，不改变"以 Inspector 引用为主"的设计。

**对你现有项目的影响：** 不需要改主角的 Tag 或 Layer，不需要动 `ProjectSettings/TagManager.asset`。

### 8.6 Phase 07 —— Drop

```csharp
public class DropSpawner : MonoBehaviour
{
    [SerializeField] float pickupRadius = 0.5f;
    // OnEnable: EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied)
    // OnEnemyDied: 若 evt.Drop == null 或 weaponPrefab == null 则忽略
    //              否则生成掉落物
}
```

**掉落物生成（全脚本驱动，不需要你制作 prefab）：**

1. `Instantiate(weaponData.weaponPrefab, position + Vector3.up * 0.5f, rotation)`
2. 剥掉模型自带的碰撞体（避免与交互查询互相干扰）
3. `AddComponent<DroppedItem>()`，写入 `ItemData` 与数量
4. `AddComponent<BoxCollider>()`，尺寸取渲染包围盒并略微放大（供 `OverlapSphere` 命中）

**`DroppedItem`** 实现 `IInteractable`：`PromptText => "拾取"`；`Interact()` 发布 `ItemPickedUpEvent` 后 `Destroy(gameObject)`。

**本切片的边界：** Phase 08（背包）不在范围内，因此拾取后物品**不会进入任何容器**，事件发布后由调试 HUD 打印。这是与 Phase 08 的明确接口，不是遗漏。

---

## 9. 怪物 Animator 契约（由你实现）

`EnemyAI` 按以下契约驱动你的怪物状态机。**命名与主角侧完全一致**，便于你复用经验。

**Animator 参数：**

| 参数 | 类型 | 含义 |
|---|---|---|
| `Speed` | float | 0 = 待机，1 = 移动（Chase 期间写 1） |
| `Attack` | trigger | 进入攻击 |
| `GetHit` | trigger | 进入受击 |
| `Die` | trigger | 进入死亡 |

**动画事件（打在对应动画的相应帧上）：**

| 事件 | 打在哪 | 作用 |
|---|---|---|
| `AttackStart` | 攻击动画出手帧 | 打开 `attackHitbox` |
| `AttackEnd` | 攻击动画收招帧 | 关闭 `attackHitbox`，通知 `brain.OnAttackEnded()` |
| `HitEnd` | 受击动画结束帧 | 通知 `brain.OnHitEnded()` |

**状态要求：** `Idle → Chase → Attack → Hit → Dead`，`Dead` 不可回退。

**注意：** 怪物动画是 FBX 内嵌的（资源包里没有独立 `.anim` 文件）。给 FBX 内嵌剪辑加动画事件需要在 Animation 窗口里选中剪辑后添加，改动会写回 FBX 的导入设置。这一点请先确认可行，如遇到阻碍告诉我，我可以改为"用计时器估算窗口时长"的降级方案。

---

## 10. 场景接线清单（由你执行）

以下步骤需要你在 Unity 里完成。每项完成后告诉我，我会继续下一 Phase。

### 10.1 场景准备

| # | 操作 |
|---|---|
| 1 | 打开 `Assets/Lowpoly Style/Desert/DemoScene/Desert.unity` |
| 2 | 主角对象：挂上你做的 Animator Controller，并把 Animator 组件拖到 `PlayerController` 的 `animator` 字段 |
| 3 | 主角对象：添加 `CharacterController` 已有（确认 `Center`/`Height`/`Radius` 与模型匹配） |

### 10.2 NPC

| # | 操作 |
|---|---|
| 1 | 从 `Assets/RPGTinyHeroWavePBR/Prefab/npcCharacters/` 挑一个（如 `MC01`），放到 `WESTERN VILLAGE` 或 `OASIS VILLAGE` 附近 |
| 2 | **给 NPC 加一个 Collider**（`CapsuleCollider` 即可，`isTrigger` 不勾选）—— 没有碰撞体 `OverlapSphere` 找不到它 |
| 3 | NPC 上挂 `NpcInteractable` |
| 4 | NPC 上挂 `DialogueRunner`（可与 `NpcInteractable` 同一对象） |

### 10.3 怪物

| # | 操作 |
|---|---|
| 1 | 从 `Assets/RPGMonsterWave02PBR/Prefabs/Character/` 挑一种（建议 `LizardWarriorPBRDefault`，因为掉落武器有对应的 `LizardWarriorBlade`），放到远离村庄的位置 |
| 2 | 挂上你做的怪物 Animator Controller |
| 3 | 加 `CapsuleCollider`（供玩家 `Hitbox` 命中） |
| 4 | 挂 `HealthComponent`、`EnemyAI` |
| 5 | 挂一个子对象 `AttackHitbox`，加 `Hitbox` 组件，位置放在怪物身前，半径覆盖攻击范围 |
| 6 | 在 `EnemyAI` 上把 `health`、`animator`、`playerTarget`（拖主角）、`attackHitbox`、`dropWeapon`（拖 `LizardWarriorBlade` 的 `WeaponData` 资产）逐个拖好 |
| 7 | 复制成 3 只（任务要求击败 3 只） |

### 10.4 玩家侧组件

| # | 操作 |
|---|---|
| 1 | 主角对象挂 `PlayerStats`、`PlayerCombat`、`PlayerInteractionDetector` |
| 2 | 主角对象下建子对象 `AttackHitbox`，加 `Hitbox` 组件，放在身前 |
| 3 | 把 `PlayerController`、`PlayerStats`、`AttackHitbox` 拖到 `PlayerCombat` 对应字段 |

### 10.5 系统对象

| # | 操作 |
|---|---|
| 1 | 建空对象 `GameSystems`，挂 `QuestComponent`、`DropSpawner`、`DebugHud` |
| 2 | 在 `NpcInteractable` 上配置三段对话 `DialogueData`，并把 `QuestComponent` 拖进去 |
| 3 | 创建数据资产：1 个 `QuestData`（`id = KillMonsters`，`requiredAmount = 3`，`targetEnemyTypeId` 留空）、3 个 `DialogueData`（接任务前 / 任务进行中 / 任务完成后）、1 个 `WeaponData`（对应 `LizardWarriorBlade`） |

---

## 11. 调试 HUD（临时）

`Debug/DebugHud.cs`，纯 IMGUI（`OnGUI`），挂在 `GameSystems` 上。**零 prefab、零 Canvas、零接线。**

显示内容：

- **玩家**：HP、`FinalDamage`
- **任务**：标题、目标文本、进度 `1/3`、状态（未接 / 进行中 / 已完成）
- **交互提示**：`[E] 对话` / `[E] 拾取`（屏幕中下方）
- **对话**：说话人、当前句、`(i/total)`、可选项编号
- **怪物**：场景中每只怪的 `EnemyState`、HP、与玩家距离
- **事件流**：最近 8 条战斗/任务/拾取事件

对话键盘输入也由 HUD 提示（`E`/`Space` 下一句，`1`/`2`/`3` 选选项）。

**Phase 10–11 做正式 uGUI 时，整个 `Debug/` 目录删除。**

---

## 12. 测试计划（EditMode，由我实现并命令行运行）

运行方式：`Unity.exe -batchmode -runTests -testPlatform EditMode -testResults <path>`

| 测试文件 | 覆盖内容 |
|---|---|
| `EventBusTests` | 订阅后收到事件；退订后收不到；`Publish` 过程中订阅/退订不抛异常；`Clear()` 清空 |
| `DialogueSystemTests` | 逐句推进；最后一句 + 无选项 → `Ended`；有选项 → `OptionsPresented`；`TrySelectOption` 跳转 `nextDialogue`；`TrySelectOption` 无后续 → `Ended`；**同帧守卫：`TryAdvance(StartFrame)` 返回 `false` 且状态不变** |
| `QuestSystemTests` | 未接受时 `ReportKill` 不计数；接受后计数递增；达 `requiredAmount` 转 `Completed`；**完成后继续 `ReportKill` 不再计数**；`targetEnemyTypeId` 不匹配不计数；重复 `Accept` 返回 false |
| `EnemyBrainTests` | Idle→Chase→Attack→Chase 的正常流转；受伤进 Hit、`OnHitEnded` 回 Chase；**`Dead` 是终态**（之后再收 `OnDamaged`/`OnAttackEnded` 状态不变、零意图）；超出 `leashRange` 回 Idle |
| `DamageCalculatorTests` | 无武器时 `FinalDamage == baseDamage`；有武器时叠加 `WeaponData.damage` |
| `DataAssetTests` | `WeaponData`/`QuestData`/`DialogueData` 实例化后字段默认值合理（防手滑漏填） |

**这些测试不能覆盖的（必须你手动试玩）：** 打击手感、动画衔接、怪物追击观感、攻击判定范围是否合适、UI 排版、场景布局。

---

## 13. 验收标准

每个 Phase 完成后我会跑一次编译 + EditMode 测试，然后由你做手动验收。

| Phase | 验收标准 |
|---|---|
| 01 | 编译通过；EditMode 测试可运行（此时测试数量可为 0，重点是 asmdef 装配打通） |
| 02 | 走近 NPC → HUD 出现 `[E] 对话`；走远 → 提示消失；按 E → HUD 打印交互目标 |
| 03 | 按 E → HUD 完整播放 NPC 对话，逐句推进到结束；对话期间按左键不触发攻击 |
| 04 | 对话结束接受任务 → HUD 显示 `讨伐魔物 0/3` |
| 05 | 左键攻击怪物 → 怪物 HP 下降；同一次挥砍只扣一次血 |
| 06 | 走进怪物视野 → 怪物追过来 → 进入攻击范围后攻击玩家 → 玩家 HP 下降；怪物受伤播放受击动画 |
| 07 | 怪物 HP 归零 → 死亡动画 → 地上出现武器模型 → 走近显示 `[E] 拾取` → 按 E 模型消失、HUD 记录拾取 |

**最终端到端验收：**

> 篝火/出生点 → 找到 NPC → 按 E 对话 → 接受任务（HUD 0/3）→ 前往怪物区 → 击杀 3 只（HUD 1/3 → 2/3 → 3/3）→ HUD 显示任务完成 → 拾取掉落的武器

---

## 14. 风险与对策

| 风险 | 影响 | 对策 |
|---|---|---|
| **FBX 内嵌动画加不了动画事件** | `AttackStart`/`AttackEnd` 无法打点，攻击判定窗口无从触发 | 降级方案：`Hitbox` 改为由 `EnemyAI`/`PlayerCombat` 用计时器开合窗口（时长可在 Inspector 配）。这会牺牲一点精确度但完全可用 |
| 怪物 Animator 参数名与约定不符 | `EnemyAI` 驱动不了状态机 | 契约已前置确认；若中途变更，改 `EnemyAI` 中的参数字符串常量即可，成本很低 |
| 场景碰撞体/图层配置遗漏 | 打不掉血、找不到 NPC | `Hitbox` 与 `PlayerInteractionDetector` 均不依赖 Layer 配置；碰撞体缺失会在 Phase 02/05 验收时立刻暴露 |
| 静态 `EventBus` 跨 Play 残留 | 关闭 Domain Reload 时重复订阅、事件触发多次 | `SubsystemRegistration` 时 `Clear()`；所有订阅严格 `OnEnable`/`OnDisable` 成对 |
| 主角 Animator 未接线 | 进 Play 持续抛 `NullReferenceException`，一切验收无法进行 | 列为 §10.1 第 2 步，是所有验收的前置条件 |
| 本切片无正式 UI | 视觉表现粗糙 | 调试 HUD 覆盖全部验收所需信息；Phase 10–11 替换 |
| 项目不是 git 仓库 | 改动无版本兜底，误改无法回滚 | 建议 `git init`（`.gitignore` 已就绪）。若你不同意，我会在每个 Phase 前后手动快照到 `docs/superpowers/sdd/snaps/` |

---

## 15. 后续（Phase 08–14，本次不做）

| Phase | 内容 | 与本切片的接口 |
|---|---|---|
| 08 | Inventory System | `InventorySystem` 订阅 `ItemPickedUpEvent`；`DroppedItem` 无需改动 |
| 09 | Equipment System | `PlayerStats.SetWeapon()` 已是接入点；`WeaponData.socketLocal*` 已定义 |
| 10 | Inventory UI / Weapon Detail UI | 替换调试 HUD 的对应部分 |
| 11 | Quest UI / Dialogue UI / HUD | 删除 `Debug/` 目录；`DialogueRunner` 的键盘输入改为鼠标点击 |
| 12–14 | 联调 / Bug Fix / 打磨 | — |

---

## 附：待你确认的事项

1. **本文档未提交 git**（项目不是 git 仓库）。是否 `git init`？
2. **§9 的怪物动画事件**——FBX 内嵌剪辑上打动画事件是否可行，请先试一下，不行我走降级方案。
3. **§10 的接线清单**——工作量集中在你这边（摆 NPC、怪物、挂组件、配数据资产）。若你觉得太重，我可以写一个 Editor 辅助脚本，把"添加组件 + 设置 SerializeField 引用"自动化，你只需要负责摆位置。
