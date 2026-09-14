## Task 13: PlayerCameraRig 与 LockOnTarget

锁定目标的搜索、切换与相机切换。目标选择的打分逻辑抽成纯函数，可测。

**Files:**
- Create: `Assets/Script/Player/LockOnTarget.cs`
- Create: `Assets/Script/Player/PlayerCameraRig.cs`
- Test: `Assets/Tests/EditMode/LockOnSelectorTests.cs`

**Interfaces:**
- Consumes: `Cinemachine.CinemachineFreeLook`, `Cinemachine.CinemachineVirtualCamera`
- Produces:
  - `Demo.LockOnTarget`（含 `IsValid -> bool`）
  - `Demo.PlayerCameraRig.CurrentTarget -> Transform`
  - `Demo.PlayerCameraRig.ToggleLockOn()`
  - `Demo.PlayerCameraRig.SelectBest(Vector3, Vector3, LockOnCandidate[], float) -> Transform`（静态）

- [ ] **Step 1: 写测试**

`Assets/Tests/EditMode/LockOnSelectorTests.cs`：

```csharp
using NUnit.Framework;
using UnityEngine;
using Demo;

namespace Demo.Tests
{
    public class LockOnSelectorTests
    {
        private GameObject _a, _b, _c;

        [SetUp]
        public void SetUp()
        {
            _a = new GameObject("A");
            _b = new GameObject("B");
            _c = new GameObject("C");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
            Object.DestroyImmediate(_c);
        }

        private static LockOnCandidate Cand(Transform t, Vector3 pos, bool visible = true)
            => new LockOnCandidate { Target = t, Position = pos, Visible = visible };

        [Test]
        public void 无候选_返回空()
        {
            var r = PlayerCameraRig.SelectBest(
                Vector3.zero, Vector3.forward, new LockOnCandidate[0], 20f);
            Assert.IsNull(r);
        }

        [Test]
        public void 超出距离的候选_被排除()
        {
            var cands = new[] { Cand(_a.transform, new Vector3(0, 0, 100f)) };
            var r = PlayerCameraRig.SelectBest(
                Vector3.zero, Vector3.forward, cands, 20f);
            Assert.IsNull(r);
        }

        [Test]
        public void 被遮挡的候选_被排除()
        {
            var cands = new[] { Cand(_a.transform, new Vector3(0, 0, 5f), visible: false) };
            var r = PlayerCameraRig.SelectBest(
                Vector3.zero, Vector3.forward, cands, 20f);
            Assert.IsNull(r);
        }

        [Test]
        public void 同样距离时_优先选更靠近镜头正前方的()
        {
            var front = Cand(_a.transform, new Vector3(0, 0, 5f));
            var side = Cand(_b.transform, new Vector3(5, 0, 0));

            var r = PlayerCameraRig.SelectBest(
                Vector3.zero, Vector3.forward, new[] { side, front }, 20f);

            Assert.AreSame(_a.transform, r, "正前方的目标应优先于侧面的");
        }

        [Test]
        public void 正前方但更远_与侧面但更近_按综合得分选择()
        {
            // 正前方 12 米 vs 正侧方 2 米，侧方综合得分应更高。
            // 校验：正面 = 距离分 0.4 × 0.6 + 角度分 1.0 × 0.4 = 0.64
            //       侧面 = 距离分 0.9 × 0.6 + 角度分 0.5 × 0.4 = 0.74 → 侧面胜
            // 正面距离须 ≥ 8.67 米，否则角度优势会反超（见 Step 4 的说明）。
            var front = Cand(_a.transform, new Vector3(0, 0, 12f));
            var side = Cand(_b.transform, new Vector3(2f, 0, 0));

            var r = PlayerCameraRig.SelectBest(
                Vector3.zero, Vector3.forward, new[] { front, side }, 20f);

            Assert.AreSame(_b.transform, r);
        }

        [Test]
        public void 只有一个合法候选_直接返回它()
        {
            var only = Cand(_c.transform, new Vector3(3f, 0, 3f));
            var r = PlayerCameraRig.SelectBest(
                Vector3.zero, Vector3.forward, new[] { only }, 20f);
            Assert.AreSame(_c.transform, r);
        }
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

Expected: 编译失败，`PlayerCameraRig` / `LockOnCandidate` 不存在。

- [ ] **Step 3: 实现 LockOnTarget**

`Assets/Script/Player/LockOnTarget.cs`：

```csharp
using UnityEngine;

namespace Demo
{
    /// <summary>
    /// 挂在可被锁定的物体上（敌人、木桩）。
    /// </summary>
    public class LockOnTarget : MonoBehaviour
    {
        [Tooltip("锁定时相机注视的点，留空则用自身位置")]
        [SerializeField] private Transform aimPoint;

        [Tooltip("该目标是否当前可被锁定")]
        [SerializeField] private bool lockable = true;

        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position;

        public bool IsValid => lockable && isActiveAndEnabled && gameObject.activeInHierarchy;
    }
}
```

- [ ] **Step 4: 实现 PlayerCameraRig**

`Assets/Script/Player/PlayerCameraRig.cs`：

```csharp
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

namespace Demo
{
    /// <summary>锁定候选。抽成结构体是为了让打分逻辑可脱离运行时被测试。</summary>
    public struct LockOnCandidate
    {
        public Transform Target;
        public Vector3 Position;
        public bool Visible;
    }

    /// <summary>
    /// 相机与锁定。管理两个 Cinemachine 相机的优先级切换、目标搜索与切换。
    /// 不碰位移，不碰 Animator。
    /// </summary>
    public class PlayerCameraRig : MonoBehaviour
    {
        [Header("Cinemachine 相机")]
        [SerializeField] private CinemachineFreeLook freeLookCamera;
        [SerializeField] private CinemachineVirtualCamera lockOnCamera;

        [Header("相机优先级")]
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 10;

        [Header("锁定参数")]
        [SerializeField] private float maxLockDistance = 18f;
        [SerializeField] private float fieldOfView = 120f;
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField] private KeyCode switchTargetKey = KeyCode.Q;

        public Transform CurrentTarget { get; private set; }
        public bool IsLockedOn => CurrentTarget != null;

        private readonly List<LockOnCandidate> _candidates = new List<LockOnCandidate>();
        private readonly Collider[] _overlapBuffer = new Collider[32];

        private void Start()
        {
            ApplyCameraPriorities();
        }

        /// <summary>由 PlayerController 每帧调用。</summary>
        public void Tick(bool toggleRequested)
        {
            if (toggleRequested)
            {
                if (CurrentTarget == null)
                    AcquireTarget();
                else
                    SwitchToNextTarget();
            }

            // 目标失效则自动解锁
            if (CurrentTarget != null && !IsStillValid(CurrentTarget))
                ClearTarget();

            ApplyCameraPriorities();
        }

        private bool IsStillValid(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                return false;

            var lt = target.GetComponent<LockOnTarget>();
            if (lt != null && !lt.IsValid)
                return false;

            return Vector3.Distance(transform.position, target.position) <= maxLockDistance;
        }

        private void AcquireTarget()
        {
            var best = FindBestTarget();
            if (best != null)
                SetTarget(best);
        }

        private void SwitchToNextTarget()
        {
            GatherCandidates();
            if (_candidates.Count == 0)
            {
                ClearTarget();
                return;
            }

            // 找到当前目标在候选列表里的位置，取下一个
            int currentIndex = _candidates.FindIndex(c => c.Target == CurrentTarget);
            int nextIndex = (currentIndex + 1) % _candidates.Count;

            SetTarget(_candidates[nextIndex].Target);
        }

        private Transform FindBestTarget()
        {
            GatherCandidates();
            return SelectBest(
                transform.position,
                freeLookCamera != null ? freeLookCamera.transform.forward : transform.forward,
                _candidates.ToArray(),
                maxLockDistance);
        }

        private void GatherCandidates()
        {
            _candidates.Clear();

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, maxLockDistance, _overlapBuffer, targetLayers);

            var camForward = freeLookCamera != null
                ? freeLookCamera.transform.forward
                : transform.forward;

            for (int i = 0; i < count; i++)
            {
                var lt = _overlapBuffer[i].GetComponentInParent<LockOnTarget>();
                if (lt == null || !lt.IsValid)
                    continue;

                var aim = lt.AimPosition;
                var toTarget = aim - transform.position;

                // 视野锥外的忽略
                if (Vector3.Angle(camForward, toTarget) > fieldOfView * 0.5f)
                    continue;

                // 视线遮挡检测
                bool visible = !Physics.Linecast(
                    transform.position + Vector3.up * 1.5f, aim, ~0, QueryTriggerInteraction.Ignore);

                _candidates.Add(new LockOnCandidate
                {
                    Target = lt.transform,
                    Position = aim,
                    Visible = visible,
                });
            }
        }

        private void SetTarget(Transform target)
        {
            CurrentTarget = target;

            if (lockOnCamera != null)
            {
                lockOnCamera.Follow = transform;
                lockOnCamera.LookAt = target;
            }
        }

        private void ClearTarget()
        {
            CurrentTarget = null;
            if (lockOnCamera != null)
                lockOnCamera.LookAt = null;
        }

        private void ApplyCameraPriorities()
        {
            bool locked = CurrentTarget != null;

            if (freeLookCamera != null)
                freeLookCamera.Priority = locked ? inactivePriority : activePriority;

            if (lockOnCamera != null)
                lockOnCamera.Priority = locked ? activePriority : inactivePriority;
        }

        /// <summary>
        /// 综合打分选择最佳目标。距离越近、越接近镜头正前方，得分越高。
        /// 抽成纯函数以便单测。
        /// </summary>
        public static Transform SelectBest(
            Vector3 playerPosition, Vector3 cameraForward,
            LockOnCandidate[] candidates, float maxDistance)
        {
            if (candidates == null || candidates.Length == 0)
                return null;

            Transform best = null;
            float bestScore = float.NegativeInfinity;

            foreach (var c in candidates)
            {
                if (c.Target == null || !c.Visible)
                    continue;

                float distance = Vector3.Distance(playerPosition, c.Position);
                if (distance > maxDistance)
                    continue;

                var toTarget = c.Position - playerPosition;
                if (toTarget.sqrMagnitude < 0.0001f)
                    continue;

                float angle = Vector3.Angle(cameraForward, toTarget);

                // 两项都归一化到 0..1，距离权重更高
                float distanceScore = 1f - Mathf.Clamp01(distance / maxDistance);
                float angleScore = 1f - Mathf.Clamp01(angle / 180f);

                float score = distanceScore * 0.6f + angleScore * 0.4f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = c.Target;
                }
            }

            return best;
        }
    }
}
```

- [ ] **Step 5: 运行测试，确认通过**

Run: Unity Test Runner → EditMode → Run All
Expected: 6 个测试全部 PASS。

两个打分测试的期望值都可手算验证，不要靠改权重去迁就失败：
正面 12m / 侧方 2m 时，正面 = 0.4×0.6 + 1.0×0.4 = 0.64，侧面 = 0.9×0.6 + 0.5×0.4 = 0.74。
若实测不符，说明 `SelectBest` 的归一化写错了，而不是权重不对。

- [ ] **Step 6: 提交**

```bash
cd "E:/unity/求职demo"
git add Assets/Script/Player/LockOnTarget.cs Assets/Script/Player/PlayerCameraRig.cs \
        Assets/Tests/EditMode/LockOnSelectorTests.cs
git commit -m "feat: 锁定系统，目标打分选择与 Cinemachine 相机切换"
```

---

