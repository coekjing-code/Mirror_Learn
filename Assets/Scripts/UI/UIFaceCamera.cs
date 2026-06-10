using UnityEngine;

// 让UI永远朝向主相机
public class UIFaceCamera : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        // 让UI看向相机 + 反转避免背面翻转
        transform.rotation = mainCamera.transform.rotation;
    }
}