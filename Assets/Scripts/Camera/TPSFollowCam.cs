using UnityEngine;

public class TPSFollowCam : MonoBehaviour
{
    [Header("Camera")]
    public Vector3 offset = new Vector3(0.5f, 0.75f, -1.75f);
    public Vector3 aimOffset = new Vector3(0.65f, 0.82f, -1.15f);
    public float mouseSensitivity = 2f;
    public float minPitch = -30f;
    public float maxPitch = 60f;

    [Header("Aim Zoom")]
    public float normalFov = 60f;
    public float aimFov = 45f;
    public float zoomSmooth = 12f;

    private PlayerController _targetPlayer;
    private Camera _camera;
    private float yaw;
    private float pitch;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_camera != null)
            normalFov = _camera.fieldOfView;
    }

    public void SetTarget(PlayerController target)
    {
        _targetPlayer = target;

        if (_targetPlayer.isLocalPlayer)
        {
            Cursor.lockState = CursorLockMode.Locked;
            yaw = _targetPlayer.transform.eulerAngles.y;
        }
    }

    public void AddRecoil(float pitchKick, float yawKick)
    {
        pitch = Mathf.Clamp(pitch - pitchKick, minPitch, maxPitch);
        yaw += yawKick;
    }

    private void LateUpdate()
    {
        if (_targetPlayer == null || !_targetPlayer.isLocalPlayer)
            return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        bool isAiming = _targetPlayer.motionCore != null && _targetPlayer.motionCore.MotionData.AimInput;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetOffset = isAiming ? aimOffset : offset;

        transform.rotation = rotation;
        transform.position = _targetPlayer.transform.position + rotation * targetOffset;
        UpdateZoom(isAiming);
    }

    private void UpdateZoom(bool isAiming)
    {
        if (_camera == null) return;

        float targetFov = isAiming ? aimFov : normalFov;
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, zoomSmooth * Time.deltaTime);
    }
}
