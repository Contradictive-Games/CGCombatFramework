namespace CG.CombatFramework
{
    [System.Serializable]
    public sealed class ScalableValue
    {
        public float Base;
        [UnityEngine.SerializeReference, SelectType] 
        public IValueScaler[] Scalers = System.Array.Empty<IValueScaler>();

        public ScalableValue()
        {
            
        }

        public ScalableValue(float baseValue)
        {
            Base = baseValue;
        }

        public float Evaluate(in EffectContext ctx)
        {
            float value = Base;
            var scalers = Scalers.OrEmpty();
            for(int i = 0; i < scalers.Length; i++)
            {
                if(scalers[i] != null) value = scalers[i].Apply(value, in ctx);
            }
            return value;
        }
    }
}