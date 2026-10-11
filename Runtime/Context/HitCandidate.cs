namespace CG.CombatFramework
{
    public readonly struct HitCandidate
    {
        public readonly IUnit Unit;
        public readonly HitData Hit;
        public readonly float Distance;

        public HitCandidate(IUnit unit, in HitData data, float distance)
        {
            Unit = unit;
            Hit = data;
            Distance = distance;
        }
    }
}