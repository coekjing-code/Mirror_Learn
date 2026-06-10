public class IdleState : BaseMotionState
{
    public IdleState(PlayerMotionCore core) : base(core) { }

    public override void EnterState() { }
    public override void ExitState() { }

    public override void UpdateState()
    {
        // 待机无移动
    }

    public override void CheckSwitchState()
    {
        // 有移动输入切行走
        if (motionData.MoveInput.magnitude > 0.1f)
        {
            if (motionData.RunInput && !motionData.AimInput)
                motionCore.SwitchState(motionCore.RunState);
            else if (motionData.CrouchInput)
                motionCore.SwitchState(motionCore.CrouchState);
            else
                motionCore.SwitchState(motionCore.WalkState);
        }
        // 空格跳
        if (motionCore.HasBufferedJump && motionCore.CanJump)
            motionCore.SwitchState(motionCore.JumpState);
    }
}
