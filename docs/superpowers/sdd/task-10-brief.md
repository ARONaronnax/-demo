## Task 10: PlayerInputReader

读键鼠输入，翻译成语义化属性。这是**唯一**接触 `UnityEngine.Input` 的地方——将来若迁移到新输入系统，只改这一个文件。

为了让死区与缩放逻辑可测，把它们抽成纯静态方法。

**Files:**
- Create: `Assets/Script/Player/PlayerInputReader.cs`
- Test: `Assets/Tests/EditMode/PlayerInputReaderTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:
  - `Demo.PlayerInputReader.MoveInput -> Vector2`
  - `Demo.PlayerInputReader.SprintHeld / LightAttackPressed / HeavyAttackPressed / RollPressed / JumpPressed / DefendHeld / LockOnPressed / SwitchTargetPressed -> bool`
  - `Demo.PlayerInputReader.ApplyDeadzone(Vector2 raw, float deadzone) -> Vector2`（静态）
  - `Demo.PlayerInputReader.SnapToEightDirections(Vector2 v) -> Vector2`（静态）

- [ ] **Step 1: 写测试**

`Assets/Tests/EditMode/PlayerInputReaderTests.cs`：

```csharp
using NUnit.Framework;
using UnityEngine;
using Demo;

namespace Demo.Tests
{
    public class PlayerInputReaderTests
    {
        [Test]
        public void 死区内的输入_被归零()
        {
            var r = PlayerInputReader.ApplyDeadzone(new Vector2(0.1f, 0.1f), 0.2f);
            Assert.AreEqual(Vector2.zero, r);
        }

        [Test]
        public void 死区外的输入_被保留并重新归一()
        {
            var r = PlayerInputReader.ApplyDeadzone(new Vector2(1f, 0f), 0.2f);
            Assert.AreEqual(1f, r.magnitude, 0.001f);
        }

        [Test]
        public void 斜向输入_合成量不超过一()
        {
            var r = PlayerInputReader.ApplyDeadzone(new Vector2(1f, 1f), 0.2f);
            Assert.LessOrEqual(r.magnitude, 1.001f);
        }

        [Test]
        public void 零输入_返回零()
        {
            Assert.AreEqual(Vector2.zero, PlayerInputReader.ApplyDeadzone(Vector2.zero, 0.2f));
        }

        [Test]
        public void 八方向吸附_斜向被吸附到四十五度()
        {
            var v = PlayerInputReader.SnapToEightDirections(new Vector2(1f, 0.9f));
            Assert.AreEqual(45f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg, 1f);
        }

        [Test]
        public void 八方向吸附_正前方不被改动()
        {
            var v = PlayerInputReader.SnapToEightDirections(new Vector2(0f, 1f));
            Assert.AreEqual(0f, v.x, 0.001f);
            Assert.AreEqual(1f, v.y, 0.001f);
        }

        [Test]
        public void 八方向吸附_零输入返回零()
        {
            Assert.AreEqual(Vector2.zero, PlayerInputReader.SnapToEightDirections(Vector2.zero));
        }
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 编译失败，`PlayerInputReader` 不存在。

- [ ] **Step 3: 实现 PlayerInputReader**

`Assets/Script/Player/PlayerInputReader.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 唯一接触 UnityEngine.Input 的地方。
    /// 把原始键鼠输入翻译成有语义的属性，其余脚本一律不直接读 Input。
    /// 将来迁移到新输入系统时，只需要重写这一个类。
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("输入手感")]
        [Tooltip("左摇杆 / WASD 的死区半径")]
        [SerializeField] private float moveDeadzone = 0.2f;

        [Tooltip("是否把移动方向吸附到八个方向。锁定战斗时开启手感更稳")]
        [SerializeField] private bool snapToEightDirections = true;

        public Vector2 MoveInput { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool LightAttackPressed { get; private set; }
        public bool HeavyAttackPressed { get; private set; }
        public bool RollPressed { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool DefendHeld { get; private set; }
        public bool LockOnPressed { get; private set; }

        private void Update()
        {
            var raw = new Vector2(
                Input.GetAxis("Horizontal"),
                Input.GetAxis("Vertical"));

            var filtered = ApplyDeadzone(raw, moveDeadzone);
            MoveInput = snapToEightDirections
                ? SnapToEightDirections(filtered)
                : filtered;

            SprintHeld = Input.GetKey(KeyCode.LeftShift);
            DefendHeld = Input.GetKey(KeyCode.F);

            LightAttackPressed = Input.GetMouseButtonDown(0);
            HeavyAttackPressed = Input.GetMouseButtonDown(1);

            RollPressed = Input.GetKeyDown(KeyCode.LeftAlt);
            JumpPressed = Input.GetKeyDown(KeyCode.Space);
            LockOnPressed = Input.GetKeyDown(KeyCode.Q);
        }

        /// <summary>
        /// 死区处理：死区内归零，死区外把剩余区间重新拉伸回 0..1，
        /// 这样摇杆离开死区的瞬间不会突然跳变。
        /// </summary>
        public static Vector2 ApplyDeadzone(Vector2 raw, float deadzone)
        {
            float magnitude = raw.magnitude;

            if (magnitude <= deadzone || magnitude <= Mathf.Epsilon)
                return Vector2.zero;

            // 把 [deadzone, 1] 映射到 [0, 1]
            float rescaled = Mathf.Clamp01((magnitude - deadzone) / (1f - deadzone));
            return raw.normalized * rescaled;
        }

        /// <summary>
        /// 把任意方向吸附到最近的 45 度倍数。
        /// 好处是混合树的权重不会在四个方向动画之间来回抖动。
        /// </summary>
        public static Vector2 SnapToEightDirections(Vector2 v)
        {
            if (v.sqrMagnitude < Mathf.Epsilon)
                return Vector2.zero;

            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            float snapped = Mathf.Round(angle / 45f) * 45f;
            float rad = snapped * Mathf.Deg2Rad;

            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * v.magnitude;
        }
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 7 个测试全部 PASS。

- [ ] **Step 5: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/PlayerInputReader.cs Assets/Tests/EditMode/PlayerInputReaderTests.cs
git commit -m "feat: PlayerInputReader，输入翻译与死区/八方向吸附"
```

---

