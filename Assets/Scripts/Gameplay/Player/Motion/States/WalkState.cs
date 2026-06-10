public class WalkState : BaseMotionState
{
    public WalkState(PlayerMotionCore core) : base(core) { }

    public override void EnterState()
    {
    }

    public override void ExitState()
    {
        
    }

    public override void UpdateState()
    {
    }

    public override void CheckSwitchState()
    {
        if (motionData.MoveInput.magnitude < 0.1f)
        {
            motionCore.SwitchState(motionCore.IdleState);
            return;
        }
        if (motionData.RunInput && !motionData.AimInput)
        {
            motionCore.SwitchState(motionCore.RunState);
            return;
        }
        if (motionData.CrouchInput && motionData.IsGrounded)
        {
            motionCore.SwitchState(motionCore.CrouchState);
            return;
        }
        if (motionCore.HasBufferedJump && motionCore.CanJump)
        {
            motionCore.SwitchState(motionCore.JumpState);
        }
    }
}
