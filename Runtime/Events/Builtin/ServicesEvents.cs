namespace CG.CombatFramework
{
    public readonly struct ScheduledEvent
    {

    }

    public readonly struct ScheduleResolvedEvent
    {

    }

    public readonly struct ScheduleCancelledEvent
    {

    }

    public readonly struct PauseChangedEvent
    {
        public readonly bool IsPaused;
        public PauseChangedEvent(bool isPaused) => IsPaused = isPaused;
    }

    public readonly struct SessionEndedEvent
    {
        public readonly DespawnReason Reason;
        public SessionEndedEvent(DespawnReason reason = DespawnReason.SessionEnd) => Reason = reason;
    }
}
