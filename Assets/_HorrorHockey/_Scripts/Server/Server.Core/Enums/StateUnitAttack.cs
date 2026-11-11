namespace Server.Core.Enums
{
    public enum StatusUnit
    {
        Idle,
        Move,
        MoveTarget,
        Attack,
        Die,
        Srun
    }

    public enum StateTarget
    {
        Null,
        Unit,
        Tower,
        Headquartre,
        Neutral
    }
}
