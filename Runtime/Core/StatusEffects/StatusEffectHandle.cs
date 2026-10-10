namespace CG.CombatFramework
{
    public readonly struct StatusEffectHandle
    {
        private readonly StatusEffectInstance instance;
        private readonly int version;

        public StatusEffectHandle(StatusEffectInstance instance)
        {
            this.instance = instance;
            version = instance != null ? instance.Version : 0;
        }

        public bool IsValid => instance != null && instance.IsActive && instance.Version == version;
        public StatusEffectInstance Get() => IsValid ? instance : null;
    }
}