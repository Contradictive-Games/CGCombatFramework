namespace CG.CombatFramework
{
    public interface IFireControl
    {
        public IUnit Owner { get; }
        public FirableDefinition Definition { get; }

        public bool CheckReady(bool raiseBlocked);
        public bool TryFire(float power = 1f, bool silent = false);
        public void EmitWithoutGates(float power = 1f);
    }
}