using UnityEngine;

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
    private bool _isHit;
    private bool _isDead;

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

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    private static readonly int JumpHash =
        Animator.StringToHash("Jump");

    private static readonly int GetHitHash =
        Animator.StringToHash("GetHit");

    private static readonly int DieHash =
        Animator.StringToHash("Die");


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
        // 攻击 / 受击时不移动
        if (_isAttacking || _isHit)
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
        // -------------------------
        // 举盾
        // 鼠标右键
        // -------------------------

        HandleDefend();

        // 如果处于受击状态
        if (_isHit)
        {
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

        // -------------------------
        // 测试
        // H / K
        // -------------------------

        HandleDebugInputs();
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

        // 攻击、翻滚、受击、跳跃时不能举盾
        if (_isAttacking ||
            _isRolling ||
            _isHit ||
            _isJumping)
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

        if (!CanPerformAction())
        {
            return;
        }

        // 如果正在举盾，不允许攻击
        if (Input.GetMouseButton(1))
        {
            return;
        }

        animator.SetTrigger(AttackHash);
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
    }


    /*
     * 把 AttackEnd 放在攻击动画结束位置
     */

    public void AttackEnd()
    {
        _isAttacking = false;
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

        // 清理当前状态
        _isAttacking = false;
        _isRolling = false;
        _isJumping = false;

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
    // 测试按键
    // =========================================================

    private void HandleDebugInputs()
    {
        // H = 受击
        if (Input.GetKeyDown(KeyCode.H))
        {
            GetHit();
        }

        // K = 死亡
        if (Input.GetKeyDown(KeyCode.K))
        {
            Die();
        }
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

        if (_isRolling)
        {
            return false;
        }

        if (_isAttacking)
        {
            return false;
        }

        if (_isHit)
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

    public bool IsGrounded
    {
        get { return _isGrounded; }
    }
}