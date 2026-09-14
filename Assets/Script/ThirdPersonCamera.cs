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

    private void Start()
    {
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);

        Vector3 targetPosition = target.position;
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition =
            targetPosition + cameraRotation * new Vector3(0f, height, -distance);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref cameraVelocity,
            smoothTime);

        transform.LookAt(targetPosition + Vector3.up * 1.2f);
    }

    private void OnValidate()
    {
        distance = Mathf.Max(0f, distance);
        smoothTime = Mathf.Max(0f, smoothTime);

        if (maxPitch < minPitch)
        {
            maxPitch = minPitch;
        }
    }
}
