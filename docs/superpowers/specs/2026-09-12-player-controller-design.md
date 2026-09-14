# 主角系统设计（子项目 ①）

- 日期：2026-09-12
- 状态：已批准，待写实现计划
- 范围：子项目 ① 中的**主角**部分（移动 / 相机 / 锁定 / 动画状态机改造）
- 不在本范围内：战斗数值与判定（②）、敌人 AI（③）、场景打磨（④）

---

## 1. 背景

项目 `求职demo` 导入了一套带完整动画和动画控制器的角色资源（RPGTinyHeroWavePBR），
已添加 Cinemachine 相机，但缺少角色控制脚本。当前的现象是：

> 把角色拖入地图后，角色会自动按顺序播放状态机里的全部动画。

本设计要解决这个问题，并搭建一个可用于求职展示的第三人称战斗角色的基础架构。

## 2. 问题根因

已核实的事实：

| 事实 | 证据 |
|---|---|
| 主角预制体 MC01 挂的是 `SwordAndShieldStance.controller` | `MC01.prefab` 的 `m_Controller` guid 解析到该文件 |
| 该控制器**参数列表为空** | `m_AnimatorParameters: []` |
| 该控制器有 44 个状态、44 条 transition | `grep -c` 统计 |
| **全部 44 条 transition 都是无条件的退出时间过渡** | 每条均为 `m_HasExitTime: 1` + `m_Conditions: []` |
| 只有 1 个状态机层，**没有任何子状态机** | `m_ChildStateMachines: []` |
| 预制体开启了根运动 | `m_ApplyRootMotion: 1` |

**结论**：这个控制器不是"坏了"，它是这套资源自带的**宣传片播放器**。
资源目录里有一个并列的 `ForShowcasing/` 文件夹，里面的 `SS.controller` 同样是零参数控制器。
作者的设计意图是让潜在买家打开场景就能看到全部动画依次播放，因此它被设计成
一条无条件的退出时间链条，且**从未打算接受任何输入**。

这也解释了为什么角色除了播放动画之外还会自己移动——`ApplyRootMotion` 是开着的。

## 3. 目标与非目标

### 目标
1. 角色动画由玩家输入驱动，而不是自动循环播放。
2. 提供自由视角移动 + 可选锁定（Lock-on）的第三人称控制器。
3. 动画状态机结构清晰、可读、可扩展，能在面试中逐条解释。
4. 为子项目 ②③ 预留好接入点，但不在本阶段实现战斗数值。

### 非目标（本阶段不做）
- 伤害判定、命中框、血量、无敌帧
- 敌人 AI、刷怪、掉落
- UI、音效、特效、存档
- 新输入系统（Input System）迁移

## 4. 已确认的决策

| 决策点 | 选择 | 理由 |
|---|---|---|
| Demo 范围 | 带战斗系统（但拆分交付） | 求职作品需要展示战斗手感 |
| 视角 | 自由视角 + 可选锁定 | 魂类标准做法，锁定只是一个状态，性价比高 |
| 状态机改造路线 | **路线 B：Editor 脚本生成** | 避免手点 100+ 条 transition；可重复生成；生成脚本本身是加分作品 |
| 位移驱动 | **混合：统一位移管线** | 走跑用代码位移保证可控，翻滚/冲刺用根运动保证不滑步 |
| 场景 | 新建 `Demo_Combat.unity` | 不动用户现有未保存场景，避免冲突 |
| 地图 | Desert | 用户选择 |
| 主角 | MC01 + OHS03_Sword + Shield01 | 资源包默认模块化角色 |
| 脚本粒度 | 核心逻辑拆成 5 个文件，职责单一 | ②③ 会持续往这套架构加东西，边界不清会失控 |
| 相机 | **Cinemachine 接管鼠标视角** | 自带阻尼、回中、防穿墙；可删掉约 50 行手写鼠标代码 |

## 5. 环境事实（已核实）

这些事实经过实际查证，不是假设：

| 项目 | 结论 |
|---|---|
| Unity 输入系统 | **旧版 Input Manager**（`activeInputHandler: 0`），`Input.GetAxis` 可用 |
| 渲染管线 | **Built-in RP**（`m_CustomRenderPipeline: 0`），`HDRP_URP/` 下的包仍是未导入的 `.zip` |
| Cinemachine | 2.10.7（CM 2.x API：`CinemachineFreeLook` / `CinemachineVirtualCamera` / `CinemachineBrain`） |
| Test Framework | 1.1.31 可用，可写 EditMode 测试 |
| 资源材质 | 全部 27 个 Lowpoly 材质 + 主角 4 个材质均使用**内置 shader** 或 `#pragma surface surf Standard`，Built-in 管线下无洋红风险 |
| 地面材质 | 已有现成的 `MAT_SAND_BASE.mat` 可直接用于竞技场地面 |
| 武器 | MC01 预制体**不含武器**，`weapon_r` / `weapon_l` 只是骨骼节点，剑盾需另外挂载 |
| 动画剪辑 | 每个位移类动画都同时提供 `InPlace/` 和 `RootMotion/` 两个版本 |
| 跳跃剪辑差异 | `InPlace/` 拆得更细（`JumpStart` / `JumpAir_Normal` / `JumpAir_Spin` / `JumpAir_Double` / `JumpEnd`），`RootMotion/` 只有 `JumpFull_Normal_RM` 和 `JumpFull_Spin_RM` |
| git | 已 `git init`，4019 个文件待跟踪，`Library/` `Temp/` `obj/` `Logs/` `UserSettings/` `.vs/` 已正确排除 |

### 5.1 资源中原作者遗留的问题

在新控制器中一并修正：

- **3 个重复的 Idle_Battle 状态**：`Idle_Battle_SwordAndShield`、`Idle_Battle_SwordAndShield 0`、`Idle_Battle_SwordAndShield 1`。造图时复制产生的冗余，合并为 1 个。
- **命名拼写错误**：`Attack01_SwordAndShiled` ~ `Attack04_SwordAndShiled`，`Shiled` 应为 `Shield`。新图中修正。

## 6. 架构设计

### 6.1 场景结构 `Demo_Combat.unity`

不复制那个 33000 行的 Desert 演示场景（瀑布粒子多、且全场景只有 9 个 MeshCollider，
大部分物件无碰撞会导致角色穿墙）。改用 Desert 包内的 **60 个 prefab** 搭一个干净的竞技场。

```
Demo_Combat
├── == Environment ==
│   ├── Ground            60×60 平面 + MeshCollider，材质 MAT_SAND_BASE
│   ├── ArenaWalls        WoodenWall1 / Fence1 / OldWall1 围一圈，留一个缺口
│   ├── Props             TorchBig、SaguaroCactus1、RockGrey1、Palmtree1、Barrel
│   └── Directional Light
├── == Camera ==
│   ├── CameraTarget      跟随玩家头部位置（只跟位置，不控旋转）
│   ├── CM_FreeLook       CinemachineFreeLook，接管鼠标自由视角
│   └── CM_LockOn         CinemachineVirtualCamera，锁定用
├── == Player ==
│   └── Player            MC01 + OHS03_Sword(→weapon_r) + Shield01(→weapon_l)
│                         + CharacterController + Animator + PlayerController
└── == Targets ==         3 个木桩（WoodenPole1）+ LockOnTarget 组件
```

`CM_FreeLook` 与 `CM_LockOn` 都激活，锁定切换通过修改 `Priority` 实现，
由 `CinemachineBrain` 自动完成混合过渡。

### 6.2 脚本架构

五个职责单一的文件。拆分的目的是让 ②③ 加入时不会失控，而不是为了形式。

| 文件 | 唯一职责 | 明确不做 |
|---|---|---|
| `PlayerInputReader.cs` | 读键鼠，翻译成语义化属性：`MoveInput` / `SprintHeld` / `LightAttackPressed` / `HeavyAttackPressed` / `RollPressed` / `DefendHeld` / `JumpPressed` / `LockOnPressed` | 不知道 Animator 存在 |
| `PlayerMotor.cs` | 位移：CharacterController + 重力 + 跳跃 + 统一位移管线 | 不知道动画状态机 |
| `PlayerAnimatorDriver.cs` | 把状态翻译成 Animator 参数，**唯一**调用 `SetFloat/SetBool/SetTrigger` 的地方 | 不碰 Transform |
| `PlayerCameraRig.cs` | 相机与锁定：vcam 切换、目标搜索、切换下一个目标 | 不碰位移 |
| `PlayerController.cs` | **门面**：持有上面四个，按顺序驱动 | 不含逻辑 |

`PlayerController.cs` 保留为门面，好处是原有的脚本引用不会断裂，
且场景里只有一个明确的入口组件。

### 6.3 统一位移管线

这是本设计的核心技术点。`OnAnimatorMove()` 作为**唯一的水平位移入口**：

```
OnAnimatorMove():
    if (当前状态是根运动状态)
        horizontalDelta = animator.deltaPosition
    else
        horizontalDelta = 代码计算的速度 * Time.deltaTime

    horizontalDelta.y = 0
    controller.Move(horizontalDelta + Vector3.up * 垂直速度 * Time.deltaTime)
```

这样避免了两处同时调用 `controller.Move()` 互相打架这个经典 bug。

**"当前状态是根运动状态"如何判定**：用 `StateMachineBehaviour`。
新增 `RootMotionTag.cs`，由生成脚本自动挂到翻滚 / 冲刺 / 跳跃 / 重连招这些
使用根运动剪辑的状态上，在 `OnStateEnter` / `OnStateExit` 翻转一个标志位。

选这个方案而不是"判断 `deltaPosition` 大小"，是因为后者是隐式约定，
换一套动画资源就会静默失效；前者是显式声明，在 Animator 窗口里能直接看见。

分工：

| 动作 | 位移来源 | 剪辑 |
|---|---|---|
| 待机 / 走 / 跑 / 冲刺 | 代码速度，与动画速度同步 | `InPlace` |
| 翻滚 / 闪避步 | 根运动 | `RootMotion` |
| 跳跃 | 代码驱动（重力 + 初速度），按 `InPlace` 分段剪辑 | `InPlace` |
| 轻攻击链 | 代码（无位移或微量前冲） | `InPlace` |
| 重攻击链 | 根运动 | `RootMotion` |

`Apply Root Motion` 保持开启。注意：一旦实现了 `OnAnimatorMove()`，
Unity 就不再自动应用根运动，必须由我们自己 `controller.Move()` 应用——
这正是本设计所依赖的行为。

### 6.4 相机与锁定

`PlayerCameraRig.cs` 的职责：

- 自由状态：`CM_FreeLook.Priority = 20`，`CM_LockOn.Priority = 10`。
  CinemachineFreeLook 自己处理鼠标输入（Axis Control 读 `Mouse X` / `Mouse Y`，
  旧版 Input Manager 下的标准轴名，可用）。
- 锁定状态：反转两个 Priority，CinemachineBrain 自动混合过去。
- 目标搜索：在玩家周围的 `LockOnTarget` 中选择，按屏幕空间距离与视线遮挡排序。
- 按 Q 在多个目标间切换；目标死亡或超出距离自动解锁。

**这是本设计中唯一会删除用户已有代码的地方**：
`PlayerController.cs` 里那段手写鼠标旋转 `CameraTarget` 的逻辑（约 50 行）会被移除，
因为 CinemachineFreeLook 接管后它会产生冲突。`CameraTarget` 保留，但退化成一个纯位置跟随点。

## 7. Animator 契约

新的 `Hero_SwordAndShield.controller` 由 Editor 脚本生成，对外契约如下。

### 7.1 参数

**移动**
| 参数 | 类型 | 说明 | ① 阶段驱动 |
|---|---|---|---|
| `Speed` | float 0..1 | 归一化平面速度 | ✅ |
| `MoveDirX` | float -1..1 | 锁定时的横向输入（相对角色） | ✅ |
| `MoveDirZ` | float -1..1 | 锁定时的纵向输入（相对角色） | ✅ |
| `IsLockedOn` | bool | 锁定中 → 切换移动混合树 | ✅ |
| `IsBattleStance` | bool | 战斗姿态（手持武器） | ✅ |
| `IsGrounded` | bool | 是否着地 | ✅ |

**动作**
| 参数 | 类型 | 说明 | ① 阶段驱动 |
|---|---|---|---|
| `LightAttack` | trigger | 轻攻击（Attack 链） | ✅ |
| `HeavyAttack` | trigger | 重攻击（Combo 链） | ✅ |
| `Roll` | trigger | 翻滚 / 闪避 | ✅ |
| `Jump` | trigger | 跳跃 | ✅ |
| `IsDefending` | bool | 按住防御 | ✅ |

**受击与特殊**（状态现在就建好，②③ 才触发）
| 参数 | 类型 | 说明 |
|---|---|---|
| `Hit` | trigger | 受击 |
| `HitIndex` | int 0..1 | 配合 `Hit` 选择 GetHit01 / GetHit02 |
| `DefendHit` | trigger | 格挡成功 |
| `Dizzy` | trigger | 眩晕 |
| `Die` | trigger | 死亡 |
| `GetUp` | trigger | 起身复活 |
| `IsDead` | bool | 死亡状态锁，阻止其他转换 |
| `Victory` / `Dance` / `LevelUp` / `Challenging` / `SenseSomething` | trigger | 特殊动作 |

**关于轻/重攻击分成两个参数**：这是对已批准设计的一处细化。
原设计只写了单个 `Attack` trigger，但资源里存在两条独立的连招链
（`Attack01-04` 四段、`Combo01-05` 五段），用一个参数无法区分。
拆成 `LightAttack` / `HeavyAttack` 后，两条链各自独立，转换条件也更直观。

### 7.2 子状态机分组

把 44 个平铺状态收进 6 个子状态机（合并重复状态后为 42 个）。transition 只在**组间**架设
（带参数条件），组内保留退出时间过渡。连线数量从 44 条降到十几条。

| 子状态机 | 包含状态 |
|---|---|
| **Locomotion** | `Idle_Normal`、`Idle_Battle`、混合树 ×3、`SprintFWD_Battle` |
| **Combat** | `Attack01`–`Attack04`、`Combo01`–`Combo05`、`Defend`（循环）、`DefendHit` |
| **Movement** | `RollFWD/BWD/LFT/RGT`、`DashFWD/BWD/LFT/RHT`、`JumpFull_Normal`、`JumpFull_Spin` |
| **Reaction** | `GetHit01`、`GetHit02`、`Dizzy` |
| **Special** | `Challenging`、`Dance`、`Victory`、`LevelUp`、`SenseSomething_Start`、`SenseSomething_Searching` |
| **Death** | `Die01` → `Die01_Stay`（躺尸循环）→ `GetUp` → `Idle_Battle`；`Die02` |

**Locomotion 内的三个混合树**——这是锁定机制的核心：

| 混合树 | 维度 | 参数 | 节点 |
|---|---|---|---|
| `Locomotion_Free_Normal` | 1D | `Speed` | `Idle_Normal` → `MoveFWD_Normal_InPlace` |
| `Locomotion_Free_Battle` | 1D | `Speed` | `Idle_Battle` → `MoveFWD_Battle_InPlace` → `SprintFWD_Battle_InPlace` |
| `Locomotion_Locked` | 2D 自由式 | `MoveDirX` / `MoveDirZ` | 中心 `Idle_Battle`，四向 `MoveFWD` / `MoveBWD` / `MoveLFT` / `MoveRGT` |

为什么需要 2D 混合树：锁定时角色**面朝敌人不转身**，所以必须靠独立的左右横移和后退动画
来表达移动。这正是资源里 `MoveBWD` / `MoveLFT` / `MoveRGT` 三个剪辑存在的意义——
它们只为锁定侧移服务。非锁定时角色自动转向移动方向，只需要一个前向动画。

### 7.3 transition 规则

**Locomotion 组内**
| 从 | 到 | 条件 |
|---|---|---|
| Idle_Normal | Locomotion_Free_Battle | `IsBattleStance == true` |
| Locomotion_Free_Battle | Idle_Normal | `IsBattleStance == false && Speed < 0.1` |
| Locomotion_Free_Battle | Locomotion_Locked | `IsLockedOn == true` |
| Locomotion_Locked | Locomotion_Free_Battle | `IsLockedOn == false` |

**出 Locomotion（进动作组）**
| 到 | 条件 |
|---|---|
| Combat / Attack01 | `LightAttack` |
| Combat / Combo01 | `HeavyAttack` |
| Movement / RollFWD、RollBWD、RollLFT、RollRGT | `Roll` 配合方向（4 条独立 transition，各自带方向条件） |
| Movement / JumpFull_Normal | `Jump` |
| Combat / Defend | `IsDefending == true` |

**回 Locomotion**
一次性动作状态都配一条 `ExitTime = 0.9`、无条件的返回 transition。
进入动作状态时通过 `StateMachineBehaviour` 关闭移动输入。

**Any State 转换**（受击打断）
| 到 | 条件 |
|---|---|
| Reaction / GetHit01 | `Hit` && `HitIndex == 0` |
| Reaction / GetHit02 | `Hit` && `HitIndex == 1` |
| Death / Die01 | `Die`（配合 `IsDead` 阻止重复进入） |

`Any State` 转换必须配置 `Can Transition To Self = false`，否则会每帧自我重入。

### 7.4 连招窗口

经典的双窗口结构，以轻攻击为例：

```
Attack01 ──[ExitTime 0.55, 条件: LightAttack]──> Attack02 ──> ... ──> Attack04
   │
   └──[ExitTime 0.95, 无条件]──> Locomotion
```

- **0.55 之前**按下攻击键 → 该 trigger 会**挂起**（Unity 的 trigger 在被某条 transition
  消费之前一直保持待命状态）。
- 到达 0.55 时，transition 被评估，挂起的 trigger 被消费，接续下一段。
- **0.95 时**若没有接续输入，无条件返回 Locomotion。

脚本侧需要一个**输入缓冲窗口（约 0.25s）**：如果玩家在攻击起手阶段按键，
这个输入应该被丢弃而不是攒着，否则会攒出一个意料之外的后续攻击。
`PlayerAnimatorDriver.cs` 负责这个丢弃逻辑。

具体的退出时间数值（0.55 / 0.95）是初始值，需要在实机中按动画实际长度微调。
生成脚本会把这些数值集中定义在文件顶部的常量区，便于统一调整。

## 8. 状态机修改清单

这是"状态机需要改什么"的完整答案。

| # | 改动 | 原状 | 改后 |
|---|---|---|---|
| 1 | 新增 Animator 参数 | **0 个** | 约 22 个（见 7.1） |
| 2 | 引入子状态机分组 | 1 层，44 状态全平铺 | 6 个子状态机，合并后 42 个状态（见 7.2） |
| 3 | transition 条件化 | 44 条全部 `m_Conditions: []` | 组间 transition 带参数条件；组内保留退出时间 |
| 4 | 合并重复状态 | 3 个 Idle_Battle 副本 | 1 个 `Idle_Battle` |
| 5 | 修正命名拼写 | `Attack*_SwordAndShiled` | `Attack*_SwordAndShield` |
| 6 | 移动改为混合树 | 单一 `MoveFWD_Battle` 状态 | Locomotion 内 3 个混合树 |
| 7 | 重构死亡链 | 展示式循环：Die01→Die01_Stay→Dizzy→GetUp→Idle→Die02 | Die01/Die02→Die01_Stay(躺尸)→GetUp→Idle |
| 8 | 挂载 StateMachineBehaviour | 无 | `RootMotionTag` + `LockMovementTag` 挂在对应状态上 |
| 9 | 新增 Any State 转换 | 无 | Any→Die / GetHit（受击打断） |
| 10 | 拆分攻击链 | 单一 Attack 链条 | 轻攻击(Attack01-04) / 重攻击(Combo01-05) 两条独立链 |
| 11 | 保留 `ApplyRootMotion` | `true` | `true`（保持，改由 `OnAnimatorMove` 接管） |
| 12 | 原 controller 处置 | — | **不动**。新生成 `Hero_SwordAndShield.controller` 独立存在 |

## 9. 两个 Editor 工具

| 工具 | 菜单项 | 作用 |
|---|---|---|
| `HeroAnimatorBuilder.cs` | `Tools/角色/生成主角状态机` | 用 `UnityEditor.Animations` API 生成 `Hero_SwordAndShield.controller`。幂等：重复运行会先删除旧的再重建 |
| `DemoSceneBuilder.cs` | `Tools/角色/搭建 Demo 场景` | 生成 `Demo_Combat.unity`，实例化地形 / prefab / 玩家 / 相机 / 靶子 |

两个工具都必须具备**幂等性**——重复执行的结果一致，不会产生重复对象。
这是它们能被安全地反复使用（以及被测试）的前提。

## 10. 交付物

```
Assets/Script/Player/
    PlayerController.cs          门面
    PlayerInputReader.cs         输入
    PlayerMotor.cs               位移
    PlayerAnimatorDriver.cs      动画参数
    PlayerCameraRig.cs           相机与锁定
    LockOnTarget.cs              挂在可锁定目标上
    RootMotionTag.cs             StateMachineBehaviour
    LockMovementTag.cs           StateMachineBehaviour
Assets/Script/Editor/
    HeroAnimatorBuilder.cs       生成状态机
    DemoSceneBuilder.cs          搭建场景
Assets/Tests/EditMode/
    HeroAnimatorBuilderTests.cs  状态机结构断言
Assets/Scenes/
    Demo_Combat.unity
docs/superpowers/specs/
    2026-09-12-player-controller-design.md   本文件
```

## 11. 验证方式

不采用"跑起来看着没问题"这种验证。分两层：

### 11.1 自动化（EditMode 测试）

针对 `HeroAnimatorBuilder` 的输出做结构断言——这是本设计里唯一能自动验证的部分，
但恰好也是最容易出错、最难肉眼检查的部分（42 个状态、几十条 transition）：

- 所有预期参数存在，且类型正确
- 全部 42 个状态（合并重复后）都被归入某个子状态机，没有遗漏；3 个混合树存在且节点完整
- 只有一个 `Idle_Battle`（重复状态已合并）
- 状态命名拼写已修正
- 每条从 Locomotion 出去的动作 transition 都有对应的返回 transition（无死路）
- 连招链完整：Attack01→02→03→04、Combo01→…→05
- `Any State` 转换的 `CanTransitionToSelf == false`
- 根运动状态上都挂了 `RootMotionTag`
- **幂等性**：连续运行两次 builder，生成结果一致（第二次不产生重复状态）

### 11.2 手动验收清单

在 Unity 中逐条执行并确认：

1. 把角色拖入场景并运行——**角色原地待机，不再自动播放动画**（原始问题的验收点）
2. 按 W/A/S/D——角色朝该方向移动，播放行走动画，方向正确
3. 按住 Shift——切换为冲刺动画，速度提升
4. 按空格——起跳、滞空、落地，动画与位移匹配
5. 按左 Alt——翻滚，位移距离与动画吻合，**无滑步**
6. 鼠标移动——镜头自由旋转，无穿墙
7. 按 Q 锁定木桩——镜头平滑过渡到锁定视角
8. 锁定状态下按 A/D——角色**横移而不转身**，播放左右横移动画
9. 锁定状态下按 S——角色后退，播放后退动画
10. 锁定状态下再次按 Q——在多个木桩间切换目标
11. 连按鼠标左键——Attack01→02→03→04 连段正确接续
12. 单独按一次左键——只出一段攻击然后回到待机
13. 连按鼠标右键——Combo 链五段正确接续

## 12. 风险与对策

| 风险 | 影响 | 对策 |
|---|---|---|
| 退出时间数值不匹配实际动画长度 | 连招手感生硬、动画被截断 | 数值集中定义在常量区，便于统一调整；验收清单第 11–13 条专门验证 |
| CinemachineFreeLook 与锁定相机的混合过渡生硬 | 手感差 | 调 Brain 的 Default Blend 与 vcam 的 Follow/LookAt 阻尼 |
| `StateMachineBehaviour` 的标志位在状态被打断时未复位 | 位移逻辑卡死 | `LockMovementTag` / `RootMotionTag` 在 `OnStateExit` 强制复位，并在 `OnStateMachineExit` 兜底 |
| 生成的 controller 与用户后续手工修改冲突 | 重跑 builder 会覆盖手工调整 | 文档中明确：builder 生成后若手工调整，不要再重跑；或先提交 git 再重跑 |
| 用户未保存的场景改动丢失 | 工作丢失 | 开写之前先让用户 `Ctrl+S`；新场景独立，不触碰现有场景 |

## 13. 后续子项目

- **② 战斗核心**：攻击判定、命中框、连招窗口数值、受击硬直、血量、无敌帧、死亡复活。
  接入点已就绪：`Hit` / `Die` / `DefendHit` 参数与 Reaction / Death 状态组已建好。
- **③ 敌人**：10 种怪物 AI。可复用 `PlayerMotor` 的位移管线与状态机的分组思路。
- **④ 场景打磨**：光照烘焙、特效、UI、音效。
