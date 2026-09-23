namespace CG.RGS.CombatFramework
{
    public interface IResourceUpdateStrategy
    {
        public float RecalculateCurrentValue(Resource resource);
    }
}