## Task 9: 两个 StateMachineBehaviour

位移管线靠这两个标记决定"这一帧的水平位移从哪来"。放在 Task 8 之前执行可以避免临时编译错误。

**Files:**
- Create: `Assets/Script/Player/RootMotionTag.cs`
- Create: `Assets/Script/Player/LockMovementTag.cs`

**Interfaces:**
- Consumes: `Demo.PlayerMotor`（Task 11 提供 `UseRootMotion` 与 `MovementLocked` 属性）
- Produces:
  - `Demo.RootMotionTag : StateMachineBehaviour`
  - `Demo.LockMovementTag : StateMachineBehaviour`

> **依赖提示**：这两个类引用 `PlayerMotor`。若先做本任务，会因 `PlayerMotor` 不存在而编译失败。
> 解决办法：先做 Task 11，再做本任务；或先做本任务并把 `PlayerMotor` 的两个属性提前建好。
> **推荐顺序：Task 11 → Task 9 → Task 8。**

- [ ] **Step 1: 实现 RootMotionTag**

`Assets/Script/Player/RootMotionTag.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 挂在所有使用根运动剪辑的状态上（翻滚、冲刺、重攻击）。
    ///
    /// PlayerMotor 在 OnAnimatorMove 里读这个标志，决定这一帧的水平位移
    /// 是用 animator.deltaPosition（根运动）还是用代码计算的速度。
    ///
    /// 为什么用 StateMachineBehaviour 而不是在代码里判断 deltaPosition 的大小：
    /// 后者是隐式约定，换一套动画资源就会静默失效；前者是显式声明，
    /// 在 Animator 窗口里能一眼看见哪些状态在用根运动。
    /// </summary>
    public class RootMotionTag : StateMachineBehaviour
    {
        // 注意：StateMachineBehaviour 是资源，所有使用该控制器的 Animator 共享同一个实例。
        // 因此绝不能把 PlayerMotor 缓存在字段里——那会把不同角色的引用串在一起。
        // 每次都从 animator 上取，这是唯一正确的做法。

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Set(animator, true);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Set(animator, false);
        }

        /// <summary>
        /// 兜底。状态机被整体退出时 Unity 不保证逐个调用 OnStateExit，
        /// 不补这一手会出现"翻滚结束后角色再也不受控"的诡异 bug。
        /// </summary>
        public override void OnStateMachineExit(Animator animator, int stateMachinePathHash)
        {
            Set(animator, false);
        }

        private static void Set(Animator animator, bool value)
        {
            var motor = animator.GetComponent<PlayerMotor>();
            if (motor != null)
                motor.UseRootMotion = value;
        }
    }
}
```

- [ ] **Step 2: 实现 LockMovementTag**

`Assets/Script/Player/LockMovementTag.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 挂在攻击、翻滚、冲刺这些动作状态上。
    ///
    /// 作用是在动作播放期间屏蔽移动输入，防止角色一边挥剑一边被 WASD 推着滑走。
    /// 与 RootMotionTag 的区别：RootMotionTag 决定"位移从哪来"，
    /// LockMovementTag 决定"这一帧还要不要接受玩家的移动输入"。
    /// 两者可以同时挂在一个状态上（翻滚就是这种情况）。
    /// </summary>
    public class LockMovementTag : StateMachineBehaviour
    {
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Set(animator, true);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Set(animator, false);
        }

        public override void OnStateMachineExit(Animator animator, int stateMachinePathHash)
        {
            Set(animator, false);
        }

        private static void Set(Animator animator, bool value)
        {
            var motor = animator.GetComponent<PlayerMotor>();
            if (motor != null)
                motor.MovementLocked = value;
        }
    }
}
```

- [ ] **Step 3: 确认编译通过**

回到 Unity，确认 Console 无编译错误。

- [ ] **Step 4: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/RootMotionTag.cs Assets/Script/Player/LockMovementTag.cs
git commit -m "feat: 新增 RootMotionTag 与 LockMovementTag 两个状态标记行为"
```

---

