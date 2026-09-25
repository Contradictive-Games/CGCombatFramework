namespace CG.RGS.CombatFramework
{
    public interface IAbility
    {
        public string Name { get; }
        public ITargetingStrategy TargetingStrategy { get; }
        public IAbilityActivationType ActivationType { get; }
        public float Cooldown { get; }
    }
}