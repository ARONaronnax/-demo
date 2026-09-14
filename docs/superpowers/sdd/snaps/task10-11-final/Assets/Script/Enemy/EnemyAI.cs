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
        private bool _warnedMissingRefs;

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
                WarnMissingRefsOnce();
                return;
            }

            EnemySensors sensors = GatherSensors();
            EnemyIntents intents = _brain.Tick(sensors);

            ApplyIntents(intents, sensors);
        }

        /// <summary>
        /// 引用没接齐时怪物会完全不动——若不提示，现象与"逻辑没生效"无法区分。
        /// 只告警一次，避免每帧刷屏。
        /// </summary>
        private void WarnMissingRefsOnce()
        {
            if (_warnedMissingRefs)
            {
                return;
            }

            _warnedMissingRefs = true;

            Debug.LogWarning(
                "[EnemyAI] " + name + " 的引用未接齐，怪物不会行动：" +
                "animator=" + (animator != null ? "OK" : "**null**") +
                " attackHitbox=" + (attackHitbox != null ? "OK" : "**null**"),
                this);
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

            if (attackHitbox != null)
            {
                attackHitbox.CloseWindow();
            }

            if (animator != null)
            {
                animator.SetTrigger(GetHitHash);
            }
        }

        private void OnDied(DamageInfo info)
        {
            _brain.Kill();
            _attackAnimPlaying = false;

            if (attackHitbox != null)
            {
                attackHitbox.CloseWindow();
            }

            if (animator != null)
            {
                animator.SetFloat(SpeedHash, 0f);
                animator.SetTrigger(DieHash);
            }

            EventBus.Publish(new EnemyDiedEvent(enemyTypeId, transform.position, dropWeapon));

            enabled = false;
        }

        // ---------- 以下方法由你怪物动画上的 Animation Event 调用 ----------

        /// <summary>攻击动画出手帧。</summary>
        public void AttackStart()
        {
            if (_brain.State == EnemyState.Dead || attackHitbox == null)
            {
                return;
            }

            attackHitbox.OpenWindow();
        }

        /// <summary>攻击动画收招帧。</summary>
        public void AttackEnd()
        {
            if (attackHitbox != null)
            {
                attackHitbox.CloseWindow();
            }

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
