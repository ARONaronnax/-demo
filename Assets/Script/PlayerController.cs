using UnityEngine;
using Demo.Combat;
using Demo.Core;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("========== 移动 ==========")]
    public float moveSpeed = 5f;
    public float rotateSmoothTime = 0.1f;

    [Header("========== 重力 ==========")]
    public float gravity = -9.81f;
    public float groundedGravity = -2f;

    [Header("========== 翻滚 ==========")]
    public float rollSpeed = 7f;
    public float rollDuration = 0.5f;

    [Header("========== 动画器 ==========")]
    public Animator animator;

    [Header("========== Root Motion ==========")]
    [Tooltip("是否让跳跃动画使用 Root Motion。")]
    public bool useJumpRootMotion = true;

    [Header("========== 连招 ==========")]
    [Tooltip("连招段数，对应 Combo01~Combo05 五段动画")]
    public int comboLength = 5;

    [Tooltip("一段播完后多少秒内再按攻击可以接下一段，超时连招重置")]
    public float comboWindow = 0.5f;

    [Tooltip("连招最后一段的伤害倍率")]
    public float finalComboMultiplier = 2f;

    [Tooltip("兜底：一段连招的最长时长。超过这么久还没收到 AttackEnd 动画事件就强制收招。" +
             "必须大于最长的一段连招动画。")]
    public float comboSegmentTimeout = 2f;

    [Header("========== 倒地 ==========")]
    [Tooltip("倒地后按空格起身。超过这么多秒没按也强制起身，防止 GetUpEnd 事件丢失导致永久躺地")]
    public float getUpTimeout = 3f;

    // =========================
    // 组件
    // =========================

    private CharacterController _cc;
    private Transform _cameraTransform;

    // =========================
    // 移动
    // =========================

    private float _targetRotation;
    private float _rotationVelocity;

    private Vector3 _velocity;
    private bool _isGrounded;

    // =========================
    // 状态
    // =========================

    private bool _isRolling;
    private bool _isAttacking;
    private bool _isJumping;
    // 受击动画播放中。只作状态标志，不参与任何操作判定——
    // 受击不产生硬直，受击期间照样能移动、攻击、翻滚、跳跃。
    private bool _isHit;
    private bool _isDead;
    private bool _isKnockedDown;

    // 连招与起身
    private ComboChain _combo;
    private bool _getUpRequested;
    private float _getUpTimer;
    private float _comboTimer;

    // =========================
    // 翻滚
    // =========================

    private float _rollTimer;
    private Vector3 _rollDir;

    // =========================
    // Animator Hash
    // =========================

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int IsDefendHash =
        Animator.StringToHash("IsDefend");

    private static readonly int IsRollHash =
        Animator.StringToHash("IsRoll");

    // 五段连招。字符串必须和 Animator 里的 Trigger 参数一字不差。
    private static readonly int[] ComboHashes =
    {
        Animator.StringToHash("Combo01_RM_SwordAndShield"),
        Animator.StringToHash("Combo02_RM_SwordAndShield"),
        Animator.StringToHash("Combo03_RM_SwordAndShield"),
        Animator.StringToHash("Combo04_RM_SwordAndShield"),
        Animator.StringToHash("Combo05_RM_SwordAndShield")
    };

    private static readonly int KnockdownHash =
        Animator.StringToHash("Knockdown");

    private static readonly int GetUpHash =
        Animator.StringToHash("GetUp");

    private static readonly int JumpHash =
        Animator.StringToHash("Jump");

    private static readonly int GetHitHash =
        Animator.StringToHash("GetHit");

    private static readonly int DieHash =
        Animator.StringToHash("Die");

    // =========================
    // 战斗窗口事件
    // =========================

    /// <summary>
    /// 攻击判定窗口打开（由攻击动画的 AttackStart 帧触发）。
    /// 参数是本次攻击的伤害倍率，连招最后一段大于 1。
    /// </summary>
    public event System.Action<float> AttackWindowOpened;

    /// <summary>攻击判定窗口关闭（由攻击动画的 AttackEnd 帧触发）。</summary>
    public event System.Action AttackWindowClosed;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }

        int length = Mathf.Clamp(comboLength, 1, ComboHashes.Length);

        _combo = new ComboChain(length, comboWindow, finalComboMultiplier);
    }


    private void Update()
    {
        UpdateGrounded();

        // 死亡以后不再接受普通操作
        if (_isDead)
        {
            ApplyGravity();
            return;
        }

        // 连招窗口计时：一段播完后太久没接就重置
        if (_combo != null)
        {
            _combo.Tick(Time.deltaTime);
        }

        // 倒地兜底：GetUpEnd 事件迟迟不来时强制起身
        TickGetUpTimer();

        // 连招兜底：AttackEnd 事件迟迟不来时强制收招
        TickComboWatchdog();

        // 输入
        CheckInputs();

        // 翻滚状态
        if (_isRolling)
        {
            RollUpdate();
        }
        else
        {
            PlayerMovement();
        }

        // 重力
        ApplyGravity();
    }


    // =========================================================
    // 地面检测
    // =========================================================

    private void UpdateGrounded()
    {
        _isGrounded = _cc.isGrounded;

        // 着地后清理垂直速度
        if (_isGrounded && _velocity.y < 0f)
        {
            _velocity.y = groundedGravity;

            // 如果已经落地，结束跳跃状态
            if (_isJumping)
            {
                _isJumping = false;
            }
        }
    }


    // =========================================================
    // 移动
    // =========================================================

    private void PlayerMovement()
    {
        // 攻击 / 倒地时不移动（受击不再锁移动）
        if (_isAttacking || _isKnockedDown)
        {
            animator.SetFloat(SpeedHash, 0f);
            return;
        }

        // 举盾时这里暂时允许移动
        // 如果你以后想做"举盾不能移动"，可以在这里限制
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector2 inputDir =
            new Vector2(horizontal, vertical);

        float inputMagnitude =
            Mathf.Clamp01(inputDir.magnitude);

        Vector3 worldMoveDir = Vector3.zero;

        if (inputMagnitude > 0.1f)
        {
            // 如果 Camera.main 运行时改变，则重新获取
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            if (_cameraTransform != null)
            {
                Vector3 camForward = _cameraTransform.forward;
                Vector3 camRight = _cameraTransform.right;

                // 不考虑摄像机上下倾斜
                camForward.y = 0f;
                camRight.y = 0f;

                camForward.Normalize();
                camRight.Normalize();

                worldMoveDir =
                    camForward * inputDir.y +
                    camRight * inputDir.x;

                worldMoveDir.Normalize();

                // =========================
                // 角色朝向移动方向
                // =========================

                _targetRotation =
                    Mathf.Atan2(
                        worldMoveDir.x,
                        worldMoveDir.z
                    ) * Mathf.Rad2Deg;

                float rotation =
                    Mathf.SmoothDampAngle(
                        transform.eulerAngles.y,
                        _targetRotation,
                        ref _rotationVelocity,
                        rotateSmoothTime
                    );

                transform.rotation =
                    Quaternion.Euler(
                        0f,
                        rotation,
                        0f
                    );

                // =========================
                // 水平移动
                // =========================

                _cc.Move(
                    worldMoveDir *
                    moveSpeed *
                    inputMagnitude *
                    Time.deltaTime
                );
            }
        }

        // Animator Speed
        animator.SetFloat(
            SpeedHash,
            inputMagnitude,
            0.1f,
            Time.deltaTime
        );
    }


    // =========================================================
    // 输入
    // =========================================================

    private void CheckInputs()
    {
        // 暂停 / 对话中屏蔽一切操作输入。
        // 只靠 timeScale = 0 不够：鼠标按键照样会被读到，
        // 攻击 / 翻滚 / 跳跃会把 Animator Trigger 排进队列，
        // 解除暂停的瞬间一次性全放出来。举盾状态也要在这里放下。
        if (InputLock.IsLocked)
        {
            animator.SetBool(IsDefendHash, false);
            return;
        }

        // -------------------------
        // 举盾
        // 鼠标右键
        // -------------------------

        HandleDefend();

        // 倒地时只吃空格（起身），不吃攻击 / 翻滚 / 跳跃
        if (_isKnockedDown)
        {
            HandleGetUpInput();
            return;
        }

        // -------------------------
        // 攻击
        // 鼠标左键
        // -------------------------

        HandleAttack();

        // -------------------------
        // 翻滚
        // C
        // -------------------------

        HandleRoll();

        // -------------------------
        // 跳跃
        // Space
        // -------------------------

        HandleJump();
    }


    // =========================================================
    // 举盾
    // =========================================================

    private void HandleDefend()
    {
        if (_isDead)
        {
            animator.SetBool(IsDefendHash, false);
            return;
        }

        bool defendInput =
            Input.GetMouseButton(1);

        // 攻击、翻滚、跳跃时不能举盾
        if (_isAttacking ||
            _isRolling ||
            _isJumping ||
            _isKnockedDown)
        {
            defendInput = false;
        }

        animator.SetBool(
            IsDefendHash,
            defendInput
        );
    }


    // =========================================================
    // 攻击
    // =========================================================

    private void HandleAttack()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (_isDead || _isKnockedDown)
        {
            return;
        }

        // 举盾时不能攻击
        if (Input.GetMouseButton(1))
        {
            return;
        }

        // 翻滚 / 跳跃中不接受攻击输入
        if (_isRolling || _isJumping)
        {
            return;
        }

        /*
         * 这里故意不用 CanPerformAction()。
         * 连招播到一半时 _isAttacking 为真，CanPerformAction() 会返回 false，
         * 那样第二次按键就永远排不进队，连招断在第二段接不上。
         */
        int index = _combo.Press();

        // 返回 -1 表示只是入队，等 AttackEnd 时再取
        if (index >= 0)
        {
            animator.SetTrigger(ComboHashes[index]);
        }

    }


    // =========================================================
    // 翻滚
    // =========================================================

    private void HandleRoll()
    {
        if (!Input.GetKeyDown(KeyCode.C))
        {
            return;
        }

        if (!CanPerformAction())
        {
            return;
        }

        if (!_isGrounded)
        {
            return;
        }

        // 举盾时不能翻滚
        if (Input.GetMouseButton(1))
        {
            return;
        }

        StartRoll();
    }


    private void StartRoll()
    {
        // 翻滚打断连招
        if (_combo != null)
        {
            _combo.Break();
        }

        _isRolling = true;
        _rollTimer = rollDuration;

        // 清理其他状态
        _isAttacking = false;
        _isHit = false;

        // 翻滚方向默认使用角色当前朝向
        _rollDir = transform.forward;

        // 如果玩家正在移动，
        // 则翻滚方向使用当前输入方向
        float horizontal =
            Input.GetAxisRaw("Horizontal");

        float vertical =
            Input.GetAxisRaw("Vertical");

        Vector2 inputDir =
            new Vector2(
                horizontal,
                vertical
            );

        if (inputDir.sqrMagnitude > 0.01f)
        {
            inputDir.Normalize();

            if (_cameraTransform == null &&
                Camera.main != null)
            {
                _cameraTransform =
                    Camera.main.transform;
            }

            if (_cameraTransform != null)
            {
                Vector3 camForward =
                    _cameraTransform.forward;

                Vector3 camRight =
                    _cameraTransform.right;

                camForward.y = 0f;
                camRight.y = 0f;

                camForward.Normalize();
                camRight.Normalize();

                _rollDir =
                    (
                        camForward * inputDir.y +
                        camRight * inputDir.x
                    ).normalized;
            }
        }

        // 翻滚方向决定角色朝向
        if (_rollDir.sqrMagnitude > 0.01f)
        {
            transform.rotation =
                Quaternion.LookRotation(
                    _rollDir
                );
        }

        animator.SetBool(
            IsRollHash,
            true
        );
    }


    private void RollUpdate()
    {
        _rollTimer -= Time.deltaTime;

        // 翻滚水平移动
        _cc.Move(
            _rollDir *
            rollSpeed *
            Time.deltaTime
        );

        // 注意：
        // 这里不 return 重力，
        // Update 最后仍然会执行 ApplyGravity()

        if (_rollTimer <= 0f)
        {
            EndRoll();
        }
    }


    private void EndRoll()
    {
        _isRolling = false;
        _rollTimer = 0f;

        animator.SetBool(
            IsRollHash,
            false
        );
    }


    // =========================================================
    // 跳跃
    // =========================================================

    private void HandleJump()
    {
        if (!Input.GetKeyDown(KeyCode.Space))
        {
            return;
        }

        if (!CanPerformAction())
        {
            return;
        }

        if (!_isGrounded)
        {
            return;
        }

        // 举盾时不能跳
        if (Input.GetMouseButton(1))
        {
            return;
        }

        StartJump();
    }


    private void StartJump()
    {
        // 跳跃打断连招
        if (_combo != null)
        {
            _combo.Break();
        }

        _isJumping = true;

        animator.SetTrigger(JumpHash);

        /*
         * 注意：
         *
         * 这里故意没有：
         *
         * _velocity.y = jumpForce;
         *
         * 因为你的设计是：
         *
         * 跳跃高度交给动画 Root Motion。
         */
    }


    // =========================================================
    // 重力
    // =========================================================

    private void ApplyGravity()
    {
        /*
         * 如果跳跃完全由 Root Motion 控制，
         * 则跳跃过程中暂时不使用普通重力。
         */
        if (_isJumping && useJumpRootMotion)
        {
            return;
        }

        if (!_isGrounded)
        {
            _velocity.y +=
                gravity *
                Time.deltaTime;
        }
        else if (_velocity.y < 0f)
        {
            _velocity.y =
                groundedGravity;
        }

        _cc.Move(
            Vector3.up *
            _velocity.y *
            Time.deltaTime
        );
    }


    // =========================================================
    // Root Motion
    // =========================================================

    private void OnAnimatorMove()
    {
        if (animator == null)
        {
            return;
        }

        /*
         * 只有跳跃状态使用 Root Motion。
         *
         * 这样普通移动仍然由 CharacterController 控制，
         * 跳跃则可以使用动画里的 Root Motion。
         */

        if (_isJumping && useJumpRootMotion)
        {
            _cc.Move(animator.deltaPosition);

            transform.rotation *=
                animator.deltaRotation;
        }
    }


    // =========================================================
    // 动画事件
    // =========================================================

    /*
     * 把 AttackStart 放在攻击动画开始位置
     */

    public void AttackStart()
    {
        if (_isDead)
        {
            return;
        }

        _isAttacking = true;
        _comboTimer = 0f;

        if (AttackWindowOpened != null)
        {
            AttackWindowOpened(_combo != null ? _combo.DamageMultiplier : 1f);
        }
    }


    /*
     * 把 AttackEnd 放在攻击动画结束位置
     */

    public void AttackEnd()
    {
        _isAttacking = false;
        _comboTimer = 0f;

        if (AttackWindowClosed != null)
        {
            AttackWindowClosed();
        }

        if (_combo == null)
        {
            return;
        }

        // 排队模式：当前段播完，才真正切到下一段
        int next = _combo.Release();

        if (next >= 0)
        {
            _isAttacking = true;
            animator.SetTrigger(ComboHashes[next]);
        }
    }


    /*
     * 在跳跃动画真正开始 Root Motion 的位置调用
     */

    public void JumpStart()
    {
        if (_isDead)
        {
            return;
        }

        _isJumping = true;
    }


    /*
     * 在跳跃动画结束的位置调用
     */

    public void JumpEnd()
    {
        _isJumping = false;
    }


    // =========================================================
    // 受击
    // =========================================================

    public void GetHit()
    {
        if (_isDead)
        {
            return;
        }

        // 连招期间霸体：伤害照吃，但不播受击动画、不打退，
        // 连招计数和当前这一段都不受影响，按左键直接接下一段。
        //
        // 判定用 _isAttacking（动画已经开打）或 _combo.IsPlaying
        // （刚按下、动画还在过渡中），两者拼起来覆盖整段连招不留空隙。
        if (_isAttacking || (_combo != null && _combo.IsPlaying))
        {
            return;
        }

        // 走到这里必然不在连招中，清掉翻滚 / 跳跃
        _isRolling = false;
        _isJumping = false;

        // 兜底：清掉可能残留的连招计时状态
        if (_combo != null)
        {
            _combo.Break();
        }

        _isHit = true;

        animator.SetBool(
            IsRollHash,
            false
        );

        animator.SetBool(
            IsDefendHash,
            false
        );

        animator.SetTrigger(
            GetHitHash
        );
    }


    /*
     * 在受击动画结束的位置添加 Animation Event
     */

    public void HitEnd()
    {
        _isHit = false;
    }


    // =========================================================
    // 死亡
    // =========================================================

    public void Die()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;

        // 清理所有动作状态
        _isAttacking = false;
        _isRolling = false;
        _isJumping = false;
        _isHit = false;
        _isKnockedDown = false;
        _getUpRequested = false;
        _getUpTimer = 0f;

        if (_combo != null)
        {
            _combo.Break();
        }

        animator.SetBool(
            IsRollHash,
            false
        );

        animator.SetBool(
            IsDefendHash,
            false
        );

        animator.SetFloat(
            SpeedHash,
            0f
        );

        animator.SetTrigger(
            DieHash
        );
    }


    // =========================================================
    // 动作条件
    // =========================================================

    private bool CanPerformAction()
    {
        if (_isDead)
        {
            return false;
        }

        if (_isKnockedDown)
        {
            return false;
        }

        if (_isRolling)
        {
            return false;
        }

        if (_isAttacking)
        {
            return false;
        }

        if (_isJumping)
        {
            return false;
        }

        return true;
    }


    // =========================================================
    // 倒地 / 起身
    // =========================================================

    /// <summary>被重击击倒。由 PlayerHealthBridge 在伤害带击倒标记时调用。</summary>
    public void Knockdown()
    {
        if (_isDead || _isKnockedDown)
        {
            return;
        }

        _isAttacking = false;
        _isRolling = false;
        _isJumping = false;
        _isHit = false;

        _getUpRequested = false;
        _getUpTimer = 0f;
        _isKnockedDown = true;

        if (_combo != null)
        {
            _combo.Break();
        }

        animator.SetBool(IsRollHash, false);
        animator.SetBool(IsDefendHash, false);
        animator.SetFloat(SpeedHash, 0f);
        animator.SetTrigger(KnockdownHash);
    }

    /// <summary>起身动画结束帧。由 GetUp 动画上的 Animation Event 调用。</summary>
    public void GetUpEnd()
    {
        _getUpRequested = false;
        _getUpTimer = 0f;
        _isKnockedDown = false;
    }

    private void HandleGetUpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartGetUp();
        }
    }

    private void StartGetUp()
    {
        if (_getUpRequested)
        {
            return;
        }

        _getUpRequested = true;
        _getUpTimer = 0f;

        animator.SetTrigger(GetUpHash);
    }

    /*
     * 起身兜底。
     *
     * 起身唯一的正常出口是 GetUpEnd 动画事件。
     * 事件一丢（动画没接好、被打断、忘记打事件），
     * _isKnockedDown 就永远为真，CanPerformAction 恒为 false，
     * 角色会永远躺在地上无法操作。
     * 所以这里加一条与动画无关的超时出口。
     */
    private void TickGetUpTimer()
    {
        if (!_isKnockedDown || !_getUpRequested)
        {
            return;
        }

        _getUpTimer += Time.deltaTime;

        if (_getUpTimer >= getUpTimeout)
        {
            GetUpEnd();
        }
    }

    /*
     * 连招兜底。
     *
     * 每一段连招的正常出口是 AttackEnd 动画事件。
     * 只要有一段动画忘了打这个事件，_isAttacking 就永远为真，
     * CanPerformAction 恒为 false，表现为"只能移动，不能翻滚 / 跳跃 / 攻击"。
     * 这里补一条与动画无关的超时出口。
     */
    private void TickComboWatchdog()
    {
        if (!_isAttacking || _combo == null)
        {
            _comboTimer = 0f;
            return;
        }

        _comboTimer += Time.deltaTime;

        if (_comboTimer >= comboSegmentTimeout)
        {
            Debug.LogWarning(
                "[PlayerController] 连招动画的 AttackEnd 事件超过 " + comboSegmentTimeout +
                " 秒没有触发，已强制收招。检查 Combo01~Combo05 每段动画上是否都打了 AttackEnd 事件。",
                this);

            AttackEnd();
        }
    }


    // =========================================================
    // 提供给其他脚本查询
    // =========================================================

    public bool IsDead
    {
        get { return _isDead; }
    }

    public bool IsRolling
    {
        get { return _isRolling; }
    }

    public bool IsAttacking
    {
        get { return _isAttacking; }
    }

    public bool IsJumping
    {
        get { return _isJumping; }
    }

    public bool IsHit
    {
        get { return _isHit; }
    }

    public bool IsKnockedDown
    {
        get { return _isKnockedDown; }
    }

    public bool IsGrounded
    {
        get { return _isGrounded; }
    }
}