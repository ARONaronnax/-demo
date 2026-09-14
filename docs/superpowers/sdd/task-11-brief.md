## Task 11: PlayerMotor —— 统一位移管线

本项目的核心技术点。`OnAnimatorMove()` 是**唯一**的水平位移入口，代码位移与根运动在此汇聚。

**Files:**
- Create: `Assets/Script/Player/PlayerMotor.cs`
- Test: `Assets/Tests/EditMode/PlayerMotorTests.cs`

**Interfaces:**
- Consumes: 无（`RootMotionTag` / `LockMovementTag` 反过来依赖本类）
- Produces:
  - `Demo.PlayerMotor.UseRootMotion -> bool { get; set; }`
  - `Demo.PlayerMotor.MovementLocked -> bool { get; set; }`
  - `Demo.PlayerMotor.PlanarSpeed -> float { get; }`
  - `Demo.PlayerMotor.ResolveHorizontalDelta(bool, Vector3, Vector3, float) -> Vector3`（静态）
  - `Demo.PlayerMotor.ComputeSpeedParameter(bool, bool) -> float`（静态）
  - `Demo.PlayerMotor.SetMoveDirection(Vector3 worldDirection, bool sprinting)`
  - `Demo.PlayerMotor.Jump()`
  - `Demo.PlayerMotor.FaceDirection(Vector3 worldDirection, float deltaTime)`

- [ ] **Step 1: 写测试**

`Assets/Tests/EditMode/PlayerMotorTests.cs`：

```csharp
using NUnit.Framework;
using UnityEngine;
using Demo;

namespace Demo.Tests
{
    public class PlayerMotorTests
    {
        // ---------- ResolveHorizontalDelta ----------

        [Test]
        public void 根运动模式_采用动画位移并抹平Y轴()
        {
            var d = PlayerMotor.ResolveHorizontalDelta(
                useRootMotion: true,
                rootMotionDelta: new Vector3(1f, 0.5f, 2f),
                codeVelocity: new Vector3(99f, 0f, 99f),
                deltaTime: 0.02f);

            Assert.AreEqual(1f, d.x, 0.001f);
            Assert.AreEqual(0f, d.y, "水平位移必须抹掉 Y 轴，垂直方向由重力单独负责");
            Assert.AreEqual(2f, d.z, 0.001f);
        }

        [Test]
        public void 代码模式_采用速度乘以时间()
        {
            var d = PlayerMotor.ResolveHorizontalDelta(
                useRootMotion: false,
                rootMotionDelta: new Vector3(99f, 0f, 99f),
                codeVelocity: new Vector3(3f, 0f, 4f),
                deltaTime: 0.5f);

            Assert.AreEqual(1.5f, d.x, 0.001f);
            Assert.AreEqual(0f, d.y);
            Assert.AreEqual(2f, d.z, 0.001f);
        }

        [Test]
        public void 代码模式_速度为竖直时水平位移为零()
        {
            var d = PlayerMotor.ResolveHorizontalDelta(
                false, Vector3.zero, new Vector3(0f, -20f, 0f), 0.02f);
            Assert.AreEqual(Vector3.zero, d);
        }

        // ---------- ComputeSpeedParameter ----------

        [Test]
        public void 无输入时_Speed参数为零()
        {
            Assert.AreEqual(0f, PlayerMotor.ComputeSpeedParameter(false, false));
            Assert.AreEqual(0f, PlayerMotor.ComputeSpeedParameter(false, true));
        }

        [Test]
        public void 有输入不冲刺_Speed参数为半()
        {
            Assert.AreEqual(0.5f, PlayerMotor.ComputeSpeedParameter(true, false));
        }

        [Test]
        public void 有输入且冲刺_Speed参数为一()
        {
            Assert.AreEqual(1f, PlayerMotor.ComputeSpeedParameter(true, true));
        }

        [Test]
        public void Speed参数_始终落在混合树定义域内()
        {
            foreach (bool moving in new[] { false, true })
            foreach (bool sprint in new[] { false, true })
            {
                float v = PlayerMotor.ComputeSpeedParameter(moving, sprint);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
        }
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 编译失败，`PlayerMotor` 不存在。

- [ ] **Step 3: 实现 PlayerMotor**

`Assets/Script/Player/PlayerMotor.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 位移的唯一负责人。
    ///
    /// 核心设计：OnAnimatorMove() 是水平位移的唯一出口。无论位移来自代码速度
    /// 还是来自动画的根运动，都汇聚到那一个 controller.Move() 调用上。
    /// 这样就不会出现"两处同时调 Move 互相打架"这个经典 bug。
    ///
    /// 本类完全不知道动画状态机里有什么状态，只读两个由 StateMachineBehaviour
    /// 设置的标志位。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("速度")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 6.0f;

        [Header("转向")]
        [Tooltip("角色朝向移动方向的平滑速度。锁定时不使用")]
        [SerializeField] private float facingRotationSpeed = 12f;

        [Header("跳跃与重力")]
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -20f;

        [Header("着地检测")]
        [SerializeField] private float groundedStickVelocity = -2f;

        /// <summary>由 RootMotionTag 设置。</summary>
        public bool UseRootMotion { get; set; }

        /// <summary>由 LockMovementTag 设置。</summary>
        public bool MovementLocked { get; set; }

        /// <summary>当前水平速度大小，供动画层读取。</summary>
        public float PlanarSpeed { get; private set; }

        public bool IsGrounded => _controller.isGrounded;

        private CharacterController _controller;
        private Animator _animator;

        private Vector3 _codeVelocity;     // 水平，代码驱动
        private float _verticalVelocity;   // 垂直，始终由代码驱动
        private bool _jumpRequested;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            ApplyGravityAndJump();
        }

        /// <summary>
        /// 由 PlayerAnimatorDriver 每帧调用，告知期望的移动方向。
        /// 这里只记录意图，真正的位移在 OnAnimatorMove 里统一执行。
        /// </summary>
        public void SetMoveDirection(Vector3 worldDirection, bool sprinting)
        {
            if (MovementLocked)
            {
                _codeVelocity = Vector3.zero;
                PlanarSpeed = 0f;
                return;
            }

            worldDirection.y = 0f;
            worldDirection = Vector3.ClampMagnitude(worldDirection, 1f);

            float speed = sprinting ? sprintSpeed : moveSpeed;
            _codeVelocity = worldDirection * speed;

            // 注意：这里用的是角色"实际"移动速度而不是期望速度。
            // 根运动状态下手动置零，避免混合树误判为在移动。
            PlanarSpeed = UseRootMotion ? 0f : _codeVelocity.magnitude;
        }

        /// <summary>把角色平滑转向指定方向。锁定状态下不调用。</summary>
        public void FaceDirection(Vector3 worldDirection, float deltaTime)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f)
                return;

            var target = Quaternion.LookRotation(worldDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, target, facingRotationSpeed * deltaTime);
        }

        /// <summary>立即转向，不插值。锁定时使用。</summary>
        public void FaceDirectionImmediate(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f)
                return;
            transform.rotation = Quaternion.LookRotation(worldDirection);
        }

        public void Jump()
        {
            if (_controller.isGrounded)
                _jumpRequested = true;
        }

        private void ApplyGravityAndJump()
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = groundedStickVelocity;

            if (_jumpRequested)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _jumpRequested = false;
            }

            _verticalVelocity += gravity * Time.deltaTime;
        }

        /// <summary>
        /// 位移的唯一出口。
        ///
        /// 一旦实现了 OnAnimatorMove，Unity 就不再自动应用根运动，
        /// 必须由我们自己把它交给 CharacterController —— 这正是本设计依赖的行为。
        /// </summary>
        private void OnAnimatorMove()
        {
            if (_animator == null)
                return;

            Vector3 horizontal = ResolveHorizontalDelta(
                UseRootMotion, _animator.deltaPosition, _codeVelocity, Time.deltaTime);

            Vector3 vertical = Vector3.up * _verticalVelocity * Time.deltaTime;
            _controller.Move(horizontal + vertical);

            // 根运动状态同时接管旋转（旋转跳需要）
            if (UseRootMotion)
                transform.rotation *= _animator.deltaRotation;
        }

        /// <summary>
        /// 决定这一帧的水平位移。抽成纯函数以便脱离运行时被测试。
        /// </summary>
        public static Vector3 ResolveHorizontalDelta(
            bool useRootMotion, Vector3 rootMotionDelta, Vector3 codeVelocity, float deltaTime)
        {
            Vector3 delta = useRootMotion
                ? rootMotionDelta
                : codeVelocity * deltaTime;

            delta.y = 0f;   // 垂直方向永远由重力管线负责
            return delta;
        }

        /// <summary>
        /// 把移动状态映射到混合树的 Speed 参数。
        /// 混合树节点阈值定义：0 = 待机，0.5 = 走/跑，1 = 冲刺。
        /// </summary>
        public static float ComputeSpeedParameter(bool moving, bool sprinting)
        {
            if (!moving) return 0f;
            return sprinting ? 1f : 0.5f;
        }
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 7 个测试全部 PASS。

- [ ] **Step 5: 回到 Task 9 完成两个 Tag**

现在 `PlayerMotor` 存在了，`RootMotionTag` / `LockMovementTag` 可以编译。
按 Task 9 的步骤创建这两个文件，然后回到 Task 8。

- [ ] **Step 6: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/PlayerMotor.cs Assets/Tests/EditMode/PlayerMotorTests.cs
git commit -m "feat: PlayerMotor 统一位移管线，OnAnimatorMove 作为唯一位移出口"
```

---

