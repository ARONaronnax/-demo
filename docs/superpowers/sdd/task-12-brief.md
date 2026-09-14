## Task 12: PlayerAnimatorDriver —— 参数写入与连招缓冲

把游戏状态翻译成 Animator 参数。含连招输入缓冲，避免玩家过早按键攒出意外连段。

**Files:**
- Create: `Assets/Script/Player/PlayerAnimatorDriver.cs`
- Test: `Assets/Tests/EditMode/ComboBufferTests.cs`

**Interfaces:**
- Consumes: `Demo.AnimatorParams`, `Demo.PlayerInputReader`, `Demo.PlayerMotor`
- Produces:
  - `Demo.ComboBuffer`（纯 C# 类）
  - `Demo.ComboBuffer.ComboBuffer(float window)`
  - `Demo.ComboBuffer.Press(float now)`
  - `Demo.ComboBuffer.Consume(float now) -> bool`
  - `Demo.ComboBuffer.HasBuffered(float now) -> bool`
  - `Demo.ComboBuffer.Clear()`
  - `Demo.ComboBuffer.LastPressTime -> float`
  - `Demo.PlayerAnimatorDriver.Tick(...)`

- [ ] **Step 1: 写测试**

`Assets/Tests/EditMode/ComboBufferTests.cs`：

```csharp
using NUnit.Framework;
using Demo;

namespace Demo.Tests
{
    public class ComboBufferTests
    {
        private const float Window = 0.25f;

        [Test]
        public void 未按过键_无缓冲()
        {
            var b = new ComboBuffer(Window);
            Assert.IsFalse(b.HasBuffered(10f));
        }

        [Test]
        public void 刚按下的键_有缓冲()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);
            Assert.IsTrue(b.HasBuffered(10.1f));
        }

        [Test]
        public void 超窗口的按键_缓冲失效()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);
            Assert.IsFalse(b.HasBuffered(10.3f), "超过 0.25 秒的输入应被丢弃，否则会攒出意外连段");
        }

        [Test]
        public void 消费后_缓冲清空()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);

            Assert.IsTrue(b.Consume(10.1f));
            Assert.IsFalse(b.HasBuffered(10.1f), "消费过的输入不能再被消费一次");
        }

        [Test]
        public void 消费超窗口输入_返回假且不清空别的状态()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);
            Assert.IsFalse(b.Consume(10.5f));
        }

        [Test]
        public void 连续按键_以最后一次为准()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);
            b.Press(10.2f);
            Assert.IsTrue(b.HasBuffered(10.4f));
            Assert.AreEqual(10.2f, b.LastPressTime, 0.001f);
        }

        [Test]
        public void 手动清空_缓冲失效()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);
            b.Clear();
            Assert.IsFalse(b.HasBuffered(10.01f));
        }

        [Test]
        public void 窗口边界_恰好等于窗口时仍有效()
        {
            var b = new ComboBuffer(Window);
            b.Press(10f);
            Assert.IsTrue(b.HasBuffered(10f + Window));
        }
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 编译失败，`ComboBuffer` 不存在。

- [ ] **Step 3: 实现 ComboBuffer**

`Assets/Script/Player/ComboBuffer.cs`：

```csharp
namespace Demo
{
    /// <summary>
    /// 连招输入缓冲。纯 C# 类，不依赖 Unity，可直接单测。
    ///
    /// 为什么需要它：玩家在攻击动画早期就按下了下一段，此时 Animator 的连招窗口
    /// 还没打开。如果直接 SetTrigger，trigger 会一直挂起，等到很久以后某个转换
    /// 评估它时才被消费，表现为"莫名其妙多打了一段"。
    /// 缓冲窗口的作用就是给这类过早输入设一个保质期。
    /// </summary>
    public class ComboBuffer
    {
        private readonly float _window;
        private float _pressedAt = float.NegativeInfinity;

        public ComboBuffer(float window)
        {
            _window = window;
        }

        public float LastPressTime => _pressedAt;

        /// <summary>记录一次按键。</summary>
        public void Press(float now)
        {
            _pressedAt = now;
        }

        /// <summary>缓冲是否仍在有效期内。</summary>
        public bool HasBuffered(float now)
        {
            return now - _pressedAt <= _window;
        }

        /// <summary>
        /// 取出缓冲。有效则返回 true 并清空，否则返回 false 且不改变状态。
        /// </summary>
        public bool Consume(float now)
        {
            if (!HasBuffered(now))
                return false;

            _pressedAt = float.NegativeInfinity;
            return true;
        }

        public void Clear()
        {
            _pressedAt = float.NegativeInfinity;
        }
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 8 个测试全部 PASS。

- [ ] **Step 5: 实现 PlayerAnimatorDriver**

`Assets/Script/Player/PlayerAnimatorDriver.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 唯一调用 Animator.SetFloat / SetBool / SetTrigger 的地方。
    ///
    /// 本类不碰 Transform，也不读 Input —— 它只接收已经翻译好的状态，
    /// 然后写进 Animator。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorDriver : MonoBehaviour
    {
        [Header("连招缓冲窗口（秒）")]
        [Tooltip("攻击键按下后多久之内算有效输入。太大会攒连段，太小会吃键")]
        [SerializeField] private float comboBufferWindow = 0.25f;

        [Header("锁定时转向速度")]
        [SerializeField] private float lockOnTurnSpeed = 15f;

        private Animator _animator;
        private PlayerMotor _motor;

        private ComboBuffer _lightBuffer;
        private ComboBuffer _heavyBuffer;

        // 缓存的参数哈希，避免每帧字符串哈希开销
        private static readonly int HashSpeed = Animator.StringToHash(AnimatorParams.Speed);
        private static readonly int HashMoveDirX = Animator.StringToHash(AnimatorParams.MoveDirX);
        private static readonly int HashMoveDirZ = Animator.StringToHash(AnimatorParams.MoveDirZ);
        private static readonly int HashIsLockedOn = Animator.StringToHash(AnimatorParams.IsLockedOn);
        private static readonly int HashIsBattleStance = Animator.StringToHash(AnimatorParams.IsBattleStance);
        private static readonly int HashIsGrounded = Animator.StringToHash(AnimatorParams.IsGrounded);
        private static readonly int HashIsDefending = Animator.StringToHash(AnimatorParams.IsDefending);
        private static readonly int HashLightAttack = Animator.StringToHash(AnimatorParams.LightAttack);
        private static readonly int HashHeavyAttack = Animator.StringToHash(AnimatorParams.HeavyAttack);
        private static readonly int HashRoll = Animator.StringToHash(AnimatorParams.Roll);
        private static readonly int HashJump = Animator.StringToHash(AnimatorParams.Jump);

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _motor = GetComponent<PlayerMotor>();
            _animator.applyRootMotion = true;

            // 用序列化的窗口值构造，这样 Inspector 里调完立刻生效
            _lightBuffer = new ComboBuffer(comboBufferWindow);
            _heavyBuffer = new ComboBuffer(comboBufferWindow);

            // 开局默认非战斗姿态，避免一进场景就举着盾
            _animator.SetBool(HashIsBattleStance, false);
        }

        /// <summary>
        /// 每帧由 PlayerController 调用。
        /// </summary>
        /// <param name="input">已翻译好的输入</param>
        /// <param name="moveDirection">世界空间的期望移动方向</param>
        /// <param name="lockedOn">是否处于锁定状态</param>
        /// <param name="lockTarget">锁定目标，未锁定传 null</param>
        /// <param name="deltaTime">帧间隔</param>
        public void Tick(
            PlayerInputReader input,
            Vector3 moveDirection,
            bool lockedOn,
            Transform lockTarget,
            float deltaTime)
        {
            WriteLocomotion(input, moveDirection, lockedOn, deltaTime);
            WriteActions(input, lockedOn);
            WriteStance(input, moveDirection, lockedOn);
        }

        private void WriteLocomotion(
            PlayerInputReader input, Vector3 moveDirection, bool lockedOn, float deltaTime)
        {
            bool moving = moveDirection.sqrMagnitude > 0.0001f;
            bool sprinting = moving && input.SprintHeld && !lockedOn;

            _animator.SetFloat(HashSpeed,
                PlayerMotor.ComputeSpeedParameter(moving, sprinting));

            if (lockedOn)
            {
                // 锁定：把世界空间移动方向换算到角色本地空间，
                // 因为此时角色朝向由锁定目标决定，不随移动方向转。
                Vector3 local = transform.InverseTransformDirection(moveDirection);
                _animator.SetFloat(HashMoveDirX, local.x);
                _animator.SetFloat(HashMoveDirZ, local.z);
            }
            else
            {
                _animator.SetFloat(HashMoveDirX, 0f);
                _animator.SetFloat(HashMoveDirZ, 0f);
            }

            _animator.SetBool(HashIsLockedOn, lockedOn);
            _animator.SetBool(HashIsGrounded, _motor.IsGrounded);
        }

        private void WriteActions(PlayerInputReader input, bool lockedOn)
        {
            float now = Time.time;

            // 记录攻击输入，然后只在缓冲有效时真正触发。
            // 这样过早的按键会在窗口过期后被丢弃，而不是挂起成意外连段。
            if (input.LightAttackPressed)
            {
                _lightBuffer.Press(now);
                _animator.SetBool(HashIsBattleStance, true);
            }
            if (input.HeavyAttackPressed)
            {
                _heavyBuffer.Press(now);
                _animator.SetBool(HashIsBattleStance, true);
            }

            if (_lightBuffer.Consume(now))
                _animator.SetTrigger(HashLightAttack);

            if (_heavyBuffer.Consume(now))
                _animator.SetTrigger(HashHeavyAttack);

            if (input.RollPressed)
                _animator.SetTrigger(HashRoll);

            if (input.JumpPressed)
            {
                _animator.SetTrigger(HashJump);
                _motor.Jump();
            }

            _animator.SetBool(HashIsDefending, input.DefendHeld);
        }

        /// <summary>
        /// 姿态与朝向。非锁定时角色转向移动方向；锁定时转向敌人。
        /// </summary>
        private void WriteStance(
            PlayerInputReader input, Vector3 moveDirection, bool lockedOn)
        {
            if (lockedOn && _currentTarget != null)
            {
                _motor.FaceDirectionImmediate(_currentTarget.position - transform.position);
                return;
            }

            if (input.LightAttackPressed || input.HeavyAttackPressed)
                return; // 攻击瞬间不要抢转向，交由动画决定

            _motor.FaceDirection(moveDirection, Time.deltaTime);
        }

        private Transform _currentTarget;

        /// <summary>由 PlayerCameraRig 在锁定目标变化时调用。</summary>
        public void SetLockOnTarget(Transform target)
        {
            _currentTarget = target;
            if (target != null)
                _animator.SetBool(HashIsBattleStance, true);
        }
    }
}
```

> **关于两个参数缓存**：`_lightBuffer` / `_heavyBuffer` 在 `Awake()` 里用序列化字段
> `comboBufferWindow` 构造，所以你在 Inspector 里改窗口值后重进 Play 模式即可生效，
> 不需要改代码。

- [ ] **Step 6: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 全部 PASS。

- [ ] **Step 7: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/ComboBuffer.cs Assets/Script/Player/PlayerAnimatorDriver.cs \
        Assets/Tests/EditMode/ComboBufferTests.cs
git commit -m "feat: PlayerAnimatorDriver 参数写入与连招输入缓冲"
```

---

