using UnityEngine;

public class JumpState : BaseMotionState
{
    private float _jumpStartTime;

    public JumpState(PlayerMotionCore core) : base(core) { }

    public override void EnterState()
    {
        _jumpStartTime = Time.time;
        motionCore.StartJump();
    }

    public override void ExitState()
    {
        
    }

    public override void UpdateState()
    {
    }

    public override void CheckSwitchState()
    {
        if (Time.time - _jumpStartTime < 0.12f) return;

        if (motionData.IsGrounded && motionData.Velocity.y <= 0f)
        {
            if (motionData.MoveInput.magnitude < 0.1f)
                motionCore.SwitchState(motionCore.IdleState);
            else if (motionData.RunInput && !motionData.AimInput)
                motionCore.SwitchState(motionCore.RunState);
            else if (motionData.CrouchInput)
                motionCore.SwitchState(motionCore.CrouchState);
            else
                motionCore.SwitchState(motionCore.WalkState);
        }
    }
}
