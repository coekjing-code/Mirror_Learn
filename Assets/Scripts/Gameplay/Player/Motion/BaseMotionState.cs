public abstract class BaseMotionState
{
    protected PlayerMotionCore motionCore;
    protected MotionData motionData;

    public BaseMotionState(PlayerMotionCore core)
    {
        motionCore = core;
        motionData = core.MotionData;
    }

    // 进入状态
    public abstract void EnterState();
    // 状态持续执行
    public abstract void UpdateState();
    // 退出状态
    public abstract void ExitState();
    // 状态切换判定
    public abstract void CheckSwitchState();
}