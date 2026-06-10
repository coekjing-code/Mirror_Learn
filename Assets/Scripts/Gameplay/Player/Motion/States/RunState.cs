public class RunState : BaseMotionState
{
    public RunState(PlayerMotionCore core) : base(core) { }

    public override void EnterState()
    {
        motionData.IsRunning = true;
    }

    public override void ExitState()
    {
        motionData.IsRunning = false;
    }

    public override void UpdateState()
    {
    }

    public override void CheckSwitchState()
    {
        if (!motionData.RunInput || motionData.AimInput)
        {
            if (motionData.MoveInput.magnitude < 0.1f)
                motionCore.SwitchState(motionCore.IdleState);
            else
                motionCore.SwitchState(motionCore.WalkState);
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
