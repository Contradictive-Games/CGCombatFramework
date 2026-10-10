namespace CG.CombatFramework
{
    public interface IFireGate
    {
        public IGateState CreateState(IFireControl control);
    }

    public interface IGateState
    {
        public bool IsReady(out FireBlockReason reason);
        public void Commit();

        public void Tick(float deltaTime);
        public void Reset();
        public float Fill01 => 0f;
    }
}