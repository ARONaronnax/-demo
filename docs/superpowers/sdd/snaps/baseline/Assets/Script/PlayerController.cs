using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("========== 移动 ==========")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 6f;

    [Header("========== 人物转向 ==========")]
    [SerializeField] private float rotationSpeed = 8f;

    [Header("========== 跳跃 ==========")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;

    [Header("========== 镜头 ==========")]
    [SerializeField] private Transform cameraTarget;

    [Tooltip("镜头目标相对于人物的高度")]
    [SerializeField] private float cameraHeight = 1.5f;

    [Tooltip("鼠标左右灵敏度")]
    [SerializeField] private float mouseSensitivity = 3f;

    [Tooltip("鼠标上下灵敏度")]
    [SerializeField] private float cameraPitchSpeed = 2f;

    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    [Header("========== 鼠标 ==========")]
    [SerializeField] private bool lockCursor = true;

    private CharacterController controller;

    private Vector3 verticalVelocity;

    private float cameraYaw;
    private float cameraPitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // 如果没有手动指定 CameraTarget，自动寻找
        if (cameraTarget == null)
        {
            GameObject targetObject = GameObject.Find("CameraTarget");

            if (targetObject != null)
            {
                cameraTarget = targetObject.transform;
            }
        }

        if (cameraTarget != null)
        {
            cameraYaw = cameraTarget.eulerAngles.y;

            cameraPitch = cameraTarget.eulerAngles.x;

            if (cameraPitch > 180f)
            {
                cameraPitch -= 360f;
            }
        }

        SetCursorState();
    }

    private void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleJumpAndGravity();
        HandleCursor();
    }

    private void LateUpdate()
    {
        UpdateCameraTargetPosition();
    }

    // =========================================================
    // 鼠标控制镜头
    // =========================================================

    private void HandleMouseLook()
    {
        if (cameraTarget == null)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // 左右旋转
        cameraYaw += mouseX * mouseSensitivity;

        // 上下旋转
        cameraPitch -= mouseY * cameraPitchSpeed;

        cameraPitch = Mathf.Clamp(
            cameraPitch,
            minPitch,
            maxPitch
        );

        // 注意：
        // CameraTarget 是独立物体，所以不会受到 Player 旋转影响
        cameraTarget.rotation = Quaternion.Euler(
            cameraPitch,
            cameraYaw,
            0f
        );
    }

    // =========================================================
    // 人物移动
    // =========================================================

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 input = new Vector3(
            horizontal,
            0f,
            vertical
        );

        input = Vector3.ClampMagnitude(input, 1f);

        if (input.sqrMagnitude < 0.001f)
            return;

        // ==========================================
        // 根据镜头方向计算移动方向
        // ==========================================

        Vector3 cameraForward;

        Vector3 cameraRight;

        if (cameraTarget != null)
        {
            cameraForward = cameraTarget.forward;
            cameraRight = cameraTarget.right;
        }
        else
        {
            cameraForward = transform.forward;
            cameraRight = transform.right;
        }

        // 不考虑镜头上下角度
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection =
            cameraForward * input.z +
            cameraRight * input.x;

        moveDirection = Vector3.ClampMagnitude(
            moveDirection,
            1f
        );

        // ==========================================
        // 移动速度
        // ==========================================

        bool sprinting = Input.GetKey(KeyCode.LeftShift);

        float currentSpeed = sprinting
            ? sprintSpeed
            : moveSpeed;

        controller.Move(
            moveDirection *
            currentSpeed *
            Time.deltaTime
        );

        // ==========================================
        // 人物朝移动方向平滑转身
        // ==========================================

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    // =========================================================
    // 跳跃 + 重力
    // =========================================================

    private void HandleJumpAndGravity()
    {
        if (controller.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }

        // Space 跳跃
        if (Input.GetKeyDown(KeyCode.Space) &&
            controller.isGrounded)
        {
            verticalVelocity.y =
                Mathf.Sqrt(
                    jumpHeight *
                    -2f *
                    gravity
                );
        }

        verticalVelocity.y +=
            gravity *
            Time.deltaTime;

        controller.Move(
            verticalVelocity *
            Time.deltaTime
        );
    }

    // =========================================================
    // CameraTarget 跟随人物位置
    // =========================================================

    private void UpdateCameraTargetPosition()
    {
        if (cameraTarget == null)
            return;

        cameraTarget.position =
            transform.position +
            Vector3.up * cameraHeight;
    }

    // =========================================================
    // 鼠标锁定
    // =========================================================

    private void HandleCursor()
    {
        // ESC 解锁鼠标
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 鼠标左键重新锁定
        if (Input.GetMouseButtonDown(0))
        {
            SetCursorState();
        }
    }

    private void SetCursorState()
    {
        if (!lockCursor)
            return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}