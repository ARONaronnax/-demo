## Task 14: PlayerController 门面

把四个组件按顺序驱动起来。这是挂在预制体上的唯一入口。

**Files:**
- Create: `Assets/Script/Player/PlayerController.cs`

**Interfaces:**
- Consumes: `PlayerInputReader` / `PlayerMotor` / `PlayerAnimatorDriver` / `PlayerCameraRig`
- Produces: `Demo.PlayerController`

- [ ] **Step 1: 实现 PlayerController**

`Assets/Script/Player/PlayerController.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 门面。持有四个组件并按固定顺序驱动它们，自身不含任何逻辑。
    ///
    /// 保留这个类而不是让四个组件各自 Update，是为了明确执行顺序：
    /// 输入 → 相机/锁定 → 位移 → 动画参数。
    /// 顺序错乱会导致明显的操作延迟感。
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerAnimatorDriver))]
    [RequireComponent(typeof(PlayerCameraRig))]
    public class PlayerController : MonoBehaviour
    {
        [Header("相机")]
        [Tooltip("移动方向参照的相机。留空则用 Camera.main")]
        [SerializeField] private Transform cameraTransform;

        private PlayerInputReader _input;
        private PlayerMotor _motor;
        private PlayerAnimatorDriver _animatorDriver;
        private PlayerCameraRig _cameraRig;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _motor = GetComponent<PlayerMotor>();
            _animatorDriver = GetComponent<PlayerAnimatorDriver>();
            _cameraRig = GetComponent<PlayerCameraRig>();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 1. 相机与锁定（决定"前方"是哪个方向）
            _cameraRig.Tick(_input.LockOnPressed);
            _animatorDriver.SetLockOnTarget(_cameraRig.CurrentTarget);

            // 2. 把输入方向换算到相机空间
            Vector3 moveDirection = CameraRelative(_input.MoveInput);

            // 3. 位移意图
            bool sprinting = _input.SprintHeld && !_cameraRig.IsLockedOn;
            _motor.SetMoveDirection(moveDirection, sprinting);

            // 4. 动画参数
            _animatorDriver.Tick(
                _input, moveDirection, _cameraRig.IsLockedOn, _cameraRig.CurrentTarget, dt);
        }

        /// <summary>
        /// 把二维输入换算成相机空间下的世界方向。
        /// 这是"按 W 就是往镜头前方走"这条直觉的实现。
        /// </summary>
        private Vector3 CameraRelative(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            Vector3 forward, right;
            if (cameraTransform != null)
            {
                forward = cameraTransform.forward;
                right = cameraTransform.right;
            }
            else
            {
                forward = Vector3.forward;
                right = Vector3.right;
            }

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }
    }
}
```

- [ ] **Step 2: 确认编译通过**

回到 Unity，确认 Console 无编译错误。

- [ ] **Step 3: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/PlayerController.cs
git commit -m "feat: PlayerController 门面，固定执行顺序驱动四个组件"
```

---

