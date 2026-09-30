namespace CG.CombatFramework
{
    public interface IUnitComponent
    {
        public void Initialize(IUnit owner);
        public void Bind();
        public void Unbind();
    }
}