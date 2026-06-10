using System;
using UnityEngine;

public enum PlayerMotionState
{
    Idle = 0,
    Walk = 1,
    Run = 2,
    Jump = 3,
    Crouch = 4
}

/// <summary>
/// 统一运动数据，所有状态共享，便于全局修改扩展
/// </summary>
[Serializable]
public class MotionData
{
    // 输入
    public Vector2 MoveInput;
    public float MouseLookX;
    public bool JumpInput;
    public bool CrouchInput;
    public bool RunInput;
    public bool AimInput;

    // 速度
    public float WalkSpeed = 2.7f;
    public float RunSpeed = 5.6f;
    public float CrouchSpeed = 2f;
    public float JumpForce = 5.5f;
    public float JumpBufferTime = 0.15f;
    public float CoyoteTime = 0.12f;

    // 旋转
    public float BodyTurnSmooth = 10f;
    public float UpperBodyIKSmooth = 15f;

    // 物理
    public float Gravity = -9.81f;
    public float GroundCheckRadius = 0.3f;
    public Vector3 Velocity;

    // 状态标记
    public bool IsGrounded;
    public bool IsCrouch;
    public bool IsRunning;
    public bool IsAiming;
}
