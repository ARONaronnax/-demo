## Task 16: 端到端手动验收

自动化测试覆盖不到手感与视觉。这一任务必须**在 Unity 里实际运行**逐条确认。

**Files:** 无（纯验证）

- [ ] **Step 1: 运行场景**

打开 `Assets/Scenes/Demo_Combat.unity`，点 Play。

- [ ] **Step 2: 逐条验收**

| # | 操作 | 预期 | 通过 |
|---|---|---|---|
| 1 | 什么都不按 | 角色**原地待机，不再自动播放动画** | ☐ |
| 2 | 按 W/A/S/D | 角色朝该方向移动并播放行走动画 | ☐ |
| 3 | 按住 Shift | 切冲刺动画，速度提升 | ☐ |
| 4 | 按空格 | 起跳、滞空、落地，动画与位移匹配 | ☐ |
| 5 | 按左 Alt | 翻滚，位移与动画吻合，**无明显滑步** | ☐ |
| 6 | 移动鼠标 | 镜头自由旋转，贴墙时不自穿 | ☐ |
| 7 | 走近木桩按 Q | 镜头平滑过渡到锁定视角 | ☐ |
| 8 | 锁定下按 A/D | 角色**横移而不转身**，播放横移动画 | ☐ |
| 9 | 锁定下按 S | 角色后退，播放后退动画 | ☐ |
| 10 | 锁定下再按 Q | 在三个木桩间循环切换目标 | ☐ |
| 11 | 连按鼠标左键 | Attack01→02→03→04 正确连段 | ☐ |
| 12 | 单按一次左键 | 只出一段然后回待机 | ☐ |
| 13 | 连按鼠标右键 | Combo 五段正确连段 | ☐ |
| 14 | 按住 F | 举起盾牌保持防御姿态 | ☐ |
| 15 | 走远后目标自动解锁 | 镜头平滑切回自由视角 | ☐ |

- [ ] **Step 3: 记录未通过项**

任何一条未通过，记录现象（不是猜测原因），进入 superpowers:systematic-debugging 流程排查。

**第 1 条是本次改造的核心验收点**——它直接验证"角色不再自动按顺序播放动画"这个原始问题是否解决。

- [ ] **Step 4: 调整手感数值并提交**

验收过程中大概率需要微调以下数值，调完提交：

| 数值 | 位置 | 初始值 |
|---|---|---|
| `ComboLinkExitTime` | `HeroAnimatorBuilder.cs` | 0.55 |
| `ActionReturnExitTime` | `HeroAnimatorBuilder.cs` | 0.90 |
| `comboBufferWindow` | `PlayerAnimatorDriver` | 0.25 |
| `moveSpeed` / `sprintSpeed` | `PlayerMotor` | 3.5 / 6.0 |
| `maxLockDistance` | `PlayerCameraRig` | 18 |

**注意**：改 `HeroAnimatorBuilder.cs` 里的常量后，必须重新执行 `Tools/角色/生成主角状态机` 才会生效。

```bash
cd "E:/unity/求职demo"
git add -A
git commit -m "tune: 依据实机验收调整连招窗口与移动速度"
```

---

## 自查

### 规范覆盖检查

| 规范章节 | 覆盖任务 |
|---|---|
| 5. 环境事实 | Global Constraints |
| 5.1 资源遗留问题（拼写、重复状态） | Task 2（常量层修正）+ Task 4 Step 1 断言 |
| 6.1 场景结构 | Task 15 |
| 6.2 脚本架构（5 文件） | Task 10–14 |
| 6.3 统一位移管线 | Task 11 + Task 9 |
| 6.4 相机与锁定 | Task 13 |
| 7.1 参数（22 个） | Task 2 + Task 4 Step 3 + Task 4 断言 |
| 7.2 子状态机分组（6 组） | Task 4（骨架）+ Task 5–7（内容） |
| 7.2 三个混合树 | Task 5 |
| 7.3 transition 规则 | Task 5 + Task 6 + Task 8 |
| 7.4 连招窗口 | Task 6 + Task 12 |
| 8. 状态机修改清单 | Task 4–8 整体 |
| 9. 两个 Editor 工具 | Task 4–8 + Task 15 |
| 11.1 自动化验证 | 各任务的测试步骤 |
| 11.2 手动验收 | Task 16 |
| 12. 风险：Tag 未复位 | Task 9 `OnStateMachineExit` |
| 12. 风险：重跑覆盖手工改动 | Task 4 Step 3 注释 + Task 16 Step 4 提示 |

### 已识别的执行顺序约束

1. **Task 1 必须最先做**——没有程序集定义，所有测试无法编译。
2. **Task 11 必须先于 Task 9**——两个 Tag 引用 `PlayerMotor`。
3. **Task 9 必须先于 Task 8**——Task 8 给状态挂 Tag。
4. **Task 4 必须先于 Task 15**——场景需要 controller 已生成。

### 需要在执行中确认的不确定点

| 点 | 风险 | 确认方式 |
|---|---|---|
| `AnimatorStateMachine.AddStateMachineTransition` API 是否存在 | 生成器骨架编译失败 | Task 4 Step 4 首次编译即可暴露 |
| fbx 内 AnimationClip 的实际命名 | 剪辑加载为 null | Task 3 Step 4 的断言 + 调试输出 |
| `AddStateMachineBehaviour<T>()` 的签名 | Task 8 编译失败 | Task 8 Step 4 编译 |
| `CinemachineFreeLook.m_XAxis.m_MaxSpeed` 在 2.10.7 的字段名 | Task 15 编译失败 | Task 15 Step 2 |
| `PrefabUtility.InstantiatePrefab` 加载武器后坐标是否需要手调 | 武器挂载位置不对手感 | Task 16 目视检查 |

这些点都在计划的早期步骤里被暴露，不会堆积到最后才炸。
