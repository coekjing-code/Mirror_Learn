using UnityEngine;

public class CrouchState : BaseMotionState
{
    public CrouchState(PlayerMotionCore core) : base(core) { }

    public override void EnterState()
    {
        motionData.IsCrouch = true;
        float crouchHeight = motionCore.DefaultControllerHeight * 0.6f;
        motionCore.CharCtrl.height = crouchHeight;
        motionCore.CharCtrl.center = motionCore.DefaultControllerCenter + Vector3.down * ((motionCore.DefaultControllerHeight - crouchHeight) * 0.5f);
    }

    public override void ExitState()
    {
        motionData.IsCrouch = false;
        motionCore.CharCtrl.height = motionCore.DefaultControllerHeight;
        motionCore.CharCtrl.center = motionCore.DefaultControllerCenter;
    }

    public override void UpdateState()
    {
    }

    public override void CheckSwitchState()
    {
        if (!motionData.CrouchInput)
        {
            if (motionData.MoveInput.magnitude < 0.1f)
                motionCore.SwitchState(motionCore.IdleState);
            else if (motionData.RunInput && !motionData.AimInput)
                motionCore.SwitchState(motionCore.RunState);
            else
                motionCore.SwitchState(motionCore.WalkState);
            return;
        }
        if (motionCore.HasBufferedJump && motionCore.CanJump)
        {
            motionCore.SwitchState(motionCore.JumpState);
        }
    }
}
