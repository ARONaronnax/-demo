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
    ///
    /// 攻击节奏有两种驱动方式，见 useAnimationEvents：
    /// 动画事件驱动、计时器驱动。动画状态机没做好时用后者，
    /// 否则 Attack 状态没有任何出口，怪物会永久停在原地。
    ///
    /// 攻击动作有三个（Attack1 / Attack2 / Attack3 三个 Trigger），
    /// 每次出手随机挑一个。
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

        [Tooltip("脱战距离。追击中一旦离玩家超过这个距离就放弃，退回待机。\n" +
                 "必须大于 detectRange，两者之差就是\"脱战后再靠近多少米才会重新追\"。")]
        [SerializeField] private float leashRange = 15f;

        [SerializeField] private bool requireLineOfSight;
        [SerializeField] private LayerMask obstacleLayers = ~0;

        [Header("贴地")]
        [Tooltip("从脚底往上多少米作为射线起点。要略高于模型，坡地上才不会打空。\n" +
                 "模型原点在脚底时 0.5 就够。")]
        [SerializeField] private float groundCheckLift = 0.5f;

        [Tooltip("从起点向下探测多远。上坡浮空就调大，下台阶瞬移得太夸张就调小。\n" +
                 "注意：超出这个深度的落差，怪会悬在原地不掉下去。")]
        [SerializeField] private float groundCheckDistance = 3f;

        [Tooltip("哪些层算地面。默认全部——自身碰撞体会被下面的代码自动跳过，不用手动排除。")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("游走（待机时）")]
        [Tooltip("勾选 = 待机时在出生点附近随机走动。取消 = 原地站着。\n" +
                 "玩家死亡、或拉开到 leashRange 之外脱战后都会进待机。")]
        [SerializeField] private bool wanderWhenIdle = true;

        [Tooltip("游走半径：以出生点为圆心，随机选点不会超出这个距离。\n" +
                 "怪追远之后也会因为这个中心而在待机时慢慢溜达回出生点附近。")]
        [SerializeField] private float wanderRadius = 6f;

        [Tooltip("游走速度。建议小于 moveSpeed，否则怪溜达起来比追人还快。")]
        [SerializeField] private float wanderSpeed = 1.2f;

        [Tooltip("到达一个游走点后至少站多久再选下一个点，秒。")]
        [SerializeField] private float wanderPause = 2f;

        [Tooltip("单个游走点最多走多少秒。够不到就换下一个点，防止卡在障碍物上原地推。")]
        [SerializeField] private float wanderStepTimeout = 6f;

        [Header("身份与掉落")]
        [SerializeField] private string enemyTypeId = "Werewolf";
        [SerializeField] private WeaponData dropWeapon;

        [Header("攻击节奏")]
        [Tooltip("勾选 = 起手与收招交给动画事件 AttackStart / AttackEnd。" +
                 "取消勾选 = 由下面的时间参数驱动。动画状态机没做好时取消勾选，先测 AI。")]
        [SerializeField] private bool useAnimationEvents = true;

        [Tooltip("运行时只读，显示当前攻击实际由谁驱动")]
        [SerializeField] private bool animationDriven;

        [Tooltip("无动画模式：进入攻击后多少秒打开判定窗")]
        [SerializeField] private float attackWindup = 0.3f;

        [Tooltip("无动画模式：一次攻击总共持续多少秒")]
        [SerializeField] private float attackDuration = 1f;

        [Tooltip("动画模式兜底：AttackEnd 事件迟迟不来时最多等多少秒就强制收招。" +
                 "必须大于最长的那段攻击动画。")]
        [SerializeField] private float attackTimeout = 2f;

        [Tooltip("兜底：HitEnd 事件迟迟不来时最多多少秒就强制脱离受击。" +
                 "必须大于 GetHit 动画长度。")]
        [SerializeField] private float hitTimeout = 1.5f;

        [Tooltip("哪一段攻击会击倒玩家，0 起算。2 = Attack3，与玩家的 Die01 对应。" +
                 "设为 -1 表示三段都不击倒。")]
        [SerializeField] private int knockdownAttackIndex = 2;

        [Tooltip("两次出手之间的间隔秒数。收招后开始计时，计时走完前不会再次攻击。" +
                 "调大 = 攻击频率降低。与攻击动画长度、AttackEnd 事件都无关。")]
        [SerializeField] private float attackCooldown = 1.5f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GetHitHash = Animator.StringToHash("GetHit");
        private static readonly int DieHash = Animator.StringToHash("Die");

        // 每次出手从这三个里随机挑一个
        private static readonly int[] AttackHashes =
        {
            Animator.StringToHash("Attack1"),
            Animator.StringToHash("Attack2"),
            Animator.StringToHash("Attack3")
        };

        private EnemyBrain _brain;
        private bool _warnedMissingRefs;

        // 游走：出生点为中心随机选点走过去，到点或超时就换下一个
        private const float WanderArriveDistance = 0.5f;

        private Vector3 _homePosition;
        private Vector3 _wanderTarget;
        private bool _hasWanderTarget;
        private float _wanderPauseTimer;
        private float _wanderStepTimer;

        private readonly RaycastHit[] _groundHits = new RaycastHit[8];

        private float _attackTimer;
        private float _hitTimer;
        private bool _hitboxOpened;

        // 本次出手选中的攻击序号，0 起算
        private int _attackIndex;

        /// <summary>
        /// 攻击是否由动画事件驱动。
        /// 勾了 useAnimationEvents 但动画器上还没有 AnimatorController 时，
        /// 自动退回计时器驱动——否则怪物会永远卡在 Attack 状态。
        /// </summary>
        private bool AnimationDriven
        {
            get
            {
                return useAnimationEvents &&
                       animator != null &&
                       animator.runtimeAnimatorController != null;
            }
        }

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

            animationDriven = AnimationDriven;

            if (useAnimationEvents && !animationDriven)
            {
                Debug.LogWarning(
                    "[EnemyAI] " + name + " 勾选了动画事件驱动，但动画器上没有 AnimatorController，" +
                    "已自动改为计时器驱动攻击。做好了动画状态机并打上 AttackStart / AttackEnd " +
                    "事件后会自动切回来。",
                    this);
            }

            _brain = new EnemyBrain(
                detectRange,
                attackRange,
                leashRange,
                requireLineOfSight,
                attackCooldown);
        }

        private void Start()
        {
            _homePosition = transform.position;

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

            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }

            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        /// <summary>
        /// 玩家死亡后不再是可以追的目标。
        ///
        /// 只把 playerTarget 置空就够了：传感器随即报 HasTarget=false，
        /// EnemyBrain 会自己从 Chase/Attack 退回 Idle，既不追也不打。
        /// 这样不用给状态机加"玩家死了"这种特例，游走也顺带接管了待机。
        /// </summary>
        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            playerTarget = null;
        }

        private void Update()
        {
            // attackHitbox 缺失不再让整只怪停摆，否则测不了 AI。
            if (attackHitbox == null)
            {
                WarnMissingRefsOnce();
            }

            EnemySensors sensors = GatherSensors();
            EnemyIntents intents = _brain.Tick(sensors, Time.deltaTime);

            ApplyIntents(intents, sensors);

            // 位移只改水平方向，高度靠这里对齐到地面
            SnapToGround();

            if (_brain.State == EnemyState.Attack)
            {
                TickAttackTimer();
            }
            else if (_brain.State == EnemyState.Hit)
            {
                TickHitTimer();
            }
        }

        /// <summary>
        /// 判定窗没接时攻击不会造成伤害——不提示的话，
        /// 现象与"逻辑没生效"无法区分。只告警一次，避免每帧刷屏。
        /// </summary>
        private void WarnMissingRefsOnce()
        {
            if (_warnedMissingRefs)
            {
                return;
            }

            _warnedMissingRefs = true;

            Debug.LogWarning(
                "[EnemyAI] " + name + " 的 Attack Hitbox 没有接线，攻击不会造成伤害。",
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
            // 待机游走优先。它在 Idle 时接管移动 / 转向 / Speed，
            // 所以直接返回，不再走下面的战斗位移。
            if (ShouldWander(intents))
            {
                TickWander();
                return;
            }

            ClearWandering();

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

            SetSpeed(intents.Move ? 1f : 0f);

            if (intents.TriggerAttack)
            {
                BeginAttack();
            }
        }

        // ---------- 贴地 ----------

        /// <summary>
        /// 把怪物对齐到脚下地面。
        ///
        /// 移动是 transform 直接位移，而且方向只取水平分量（flat.y = 0），
        /// 所以高度全程没人管——地形一有起伏就会陷进去或浮在半空。
        /// 这里每帧从脚底往上一点打一条向下的射线，取最高命中点作为新的 Y。
        ///
        /// 用 RaycastNonAlloc 而不是单发 Raycast：怪物自身也有碰撞体
        /// （本体碰撞体、武器判定框），单发射线会先命中自己。取最近的非自身命中，
        /// 零配置——不用把地面单独分一个 Layer，也不用挂 CharacterController。
        /// </summary>
        private void SnapToGround()
        {
            Vector3 origin = transform.position + Vector3.up * groundCheckLift;

            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _groundHits,
                groundCheckLift + groundCheckDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float bestY = float.NegativeInfinity;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                Transform hit = _groundHits[i].transform;

                // 跳过自己身上的一切（本体碰撞体、武器判定框……）
                if (hit == null || hit.root == transform.root)
                {
                    continue;
                }

                // 取最高命中点：站在平台上时，平台顶面优先于它下方的地面
                if (_groundHits[i].point.y > bestY)
                {
                    bestY = _groundHits[i].point.y;
                    found = true;
                }
            }

            // 打不到地面就不动 Y。宁可悬浮，也不要掉出地图
            if (!found)
            {
                return;
            }

            Vector3 pos = transform.position;
            pos.y = bestY;
            transform.position = pos;
        }

        // ---------- 待机游走 ----------

        /// <summary>
        /// 只有 Idle 且没有任何战斗意图时才游走。
        /// 玩家死亡后 playerTarget 被置空，AI 会退回 Idle，于是自动改为游走——
        /// 不需要"玩家死了"这个特例分支。
        /// </summary>
        private bool ShouldWander(in EnemyIntents intents)
        {
            return wanderWhenIdle &&
                   _brain.State == EnemyState.Idle &&
                   !intents.Move &&
                   !intents.FaceTarget &&
                   !intents.TriggerAttack;
        }

        /// <summary>
        /// 待机时在出生点 wanderRadius 内随机选点走过去。
        /// 移动是 transform 直接位移，与追击共用同一套（本项目不用 NavMesh），
        /// 所以游走同样不会绕障碍物——靠 wanderStepTimeout 兜底换点。
        /// </summary>
        private void TickWander()
        {
            if (!_hasWanderTarget)
            {
                _wanderPauseTimer -= Time.deltaTime;

                if (_wanderPauseTimer > 0f)
                {
                    SetSpeed(0f);
                    return;
                }

                _wanderTarget = PickWanderPoint();
                _hasWanderTarget = true;
                _wanderStepTimer = 0f;
            }

            Vector3 flat = _wanderTarget - transform.position;
            flat.y = 0f;

            _wanderStepTimer += Time.deltaTime;

            // 走到了，或者走太久还没到（多半被障碍物卡住），都换下一个点
            if (flat.magnitude <= WanderArriveDistance ||
                _wanderStepTimer >= wanderStepTimeout)
            {
                StopWandering();
                return;
            }

            Vector3 direction = flat.normalized;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                turnSpeed * Time.deltaTime);

            transform.position += direction * wanderSpeed * Time.deltaTime;

            SetSpeed(WanderSpeedParam());
        }

        private Vector3 PickWanderPoint()
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;

            return _homePosition + new Vector3(offset.x, 0f, offset.y);
        }

        private void StopWandering()
        {
            _hasWanderTarget = false;
            _wanderStepTimer = 0f;
            _wanderPauseTimer = wanderPause;

            SetSpeed(0f);
        }

        /// <summary>
        /// 离开 Idle 时丢弃当前游走点，并把停顿计时器补满，
        /// 这样下次回到待机会先站一会儿再动，而不是立刻又走。
        /// </summary>
        private void ClearWandering()
        {
            _hasWanderTarget = false;
            _wanderStepTimer = 0f;
            _wanderPauseTimer = wanderPause;
        }

        /// <summary>
        /// 游走速度换算成 Animator 的 Speed 参数。
        /// Speed 是相对 moveSpeed 的比值，直接用 1 会让溜达播成全速跑。
        /// </summary>
        private float WanderSpeedParam()
        {
            if (moveSpeed <= 0.01f)
            {
                return 0f;
            }

            return Mathf.Clamp01(wanderSpeed / moveSpeed);
        }

        // ---------- 攻击节奏 ----------

        private void BeginAttack()
        {
            _attackTimer = 0f;
            _hitboxOpened = false;

            // 三段攻击随机挑一段。
            // 计时器模式下没有动画也要挑，否则击倒标记拿不到攻击序号。
            _attackIndex = Random.Range(0, AttackHashes.Length);

            // 必须在判定窗打开之前设置：OpenWindow 只覆盖倍率，不动击倒标记
            SetKnockdown(_attackIndex == knockdownAttackIndex);

            if (AnimationDriven)
            {
                SetTrigger(AttackHashes[_attackIndex]);
            }
        }

        /// <summary>
        /// 动画模式：只做超时兜底，真正的起手收招交给动画事件。
        /// 计时器模式：到点开判定窗，到点收招。
        /// </summary>
        private void TickAttackTimer()
        {
            _attackTimer += Time.deltaTime;

            if (AnimationDriven)
            {
                if (_attackTimer >= attackTimeout)
                {
                    EndAttack();
                }

                return;
            }

            if (!_hitboxOpened && _attackTimer >= attackWindup)
            {
                _hitboxOpened = true;
                OpenHitbox();
            }

            if (_attackTimer >= attackDuration)
            {
                EndAttack();
            }
        }

        /// <summary>
        /// 受击兜底。HitEnd 动画事件丢失时怪物会永远卡在 Hit，
        /// 表现为既不移动也不攻击。
        /// </summary>
        private void TickHitTimer()
        {
            _hitTimer += Time.deltaTime;

            if (_hitTimer >= hitTimeout)
            {
                _hitTimer = 0f;
                _brain.OnHitEnded();
            }
        }

        /// <summary>
        /// 收招：关判定窗并让状态机回到 Chase。
        /// 动画事件和计时器共用这一条出口。
        /// </summary>
        private void EndAttack()
        {
            CloseHitbox();

            _hitboxOpened = false;
            _attackTimer = 0f;

            _brain.OnAttackEnded();
        }

        // ---------- 受击与死亡 ----------

        private void OnDamaged(DamageInfo info)
        {
            _attackTimer = 0f;
            _hitTimer = 0f;
            _hitboxOpened = false;

            _brain.OnDamaged();

            CloseHitbox();

            SetTrigger(GetHitHash);
        }

        private void OnDied(DamageInfo info)
        {
            _brain.Kill();

            _attackTimer = 0f;
            _hitTimer = 0f;
            _hitboxOpened = false;

            CloseHitbox();

            SetSpeed(0f);
            SetTrigger(DieHash);

            EventBus.Publish(new EnemyDiedEvent(enemyTypeId, transform.position, dropWeapon));

            enabled = false;
        }

        // ---------- 以下方法由你怪物动画上的 Animation Event 调用 ----------

        /// <summary>攻击动画出手帧。三段攻击动画都要打。</summary>
        public void AttackStart()
        {
            if (_brain.State == EnemyState.Dead)
            {
                return;
            }

            _hitboxOpened = true;
            OpenHitbox();
        }

        /// <summary>攻击动画收招帧。三段攻击动画都要打。</summary>
        public void AttackEnd()
        {
            EndAttack();
        }

        /// <summary>受击动画结束帧。</summary>
        public void HitEnd()
        {
            _hitTimer = 0f;
            _brain.OnHitEnded();
        }

        // ---------- Animator / Hitbox 空值安全包装 ----------
        //
        // 动画组件是可选的：状态机还没做时不该让整只怪停摆。

        private void OpenHitbox()
        {
            if (attackHitbox != null)
            {
                attackHitbox.OpenWindow();
            }
        }

        private void CloseHitbox()
        {
            if (attackHitbox != null)
            {
                attackHitbox.CloseWindow();
            }
        }

        private void SetKnockdown(bool value)
        {
            if (attackHitbox != null)
            {
                attackHitbox.SetKnockdown(value);
            }
        }

        private void SetSpeed(float value)
        {
            if (animator != null)
            {
                animator.SetFloat(SpeedHash, value);
            }
        }

        private void SetTrigger(int hash)
        {
            if (animator != null)
            {
                animator.SetTrigger(hash);
            }
        }
    }
}
