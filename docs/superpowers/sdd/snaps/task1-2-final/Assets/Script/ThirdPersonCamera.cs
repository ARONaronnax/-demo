using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("目标角色")]
    public Transform target;

    [Header("相机参数")]
    public float distance = 5f;
    public float height = 2f;
    public float mouseSensitivity = 100f;
    public float smoothTime = 0.1f;

    [Header("俯仰角度限制")]
    public float minPitch = -20f;
    public float maxPitch = 60f;

    private float yaw;
    private float pitch;
    private Vector3 cameraVelocity;

    void Start()
    {
        // 初始化相机旋转角度
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        // 锁定鼠标在游戏窗口内
        Cursor.lockState = CursorLockMode.Locked;
    }

    void LateUpdate()
    {
        // 鼠标输入
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // 计算相机位置
        Quaternion cameraRot = Quaternion.Euler(pitch, yaw, 0);
        Vector3 targetPosition = target.position + cameraRot * new Vector3(0, height, -distance);

        // 平滑移动相机
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref cameraVelocity, smoothTime);
        // 看向角色上半身
        transform.LookAt(target.position + Vector3.up * 1.2f);
    }
}
