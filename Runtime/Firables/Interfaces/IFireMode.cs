
namespace CG.CombatFramework
{
    public interface IFireMode
    {
        public IFireModeState CreateState();
    }

    public interface IFireModeState
    {
        public FiringPhase Phase { get; }
        public float Progress01 => 0f;
        public void Press(IFireControl control);
        public void Release(IFireControl control){}
        public void Cancel(IFireControl control){}
        public void Tick(IFireControl control, float deltaTime){}
    }
}