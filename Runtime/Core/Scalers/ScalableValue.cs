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
            for(int i = 0; i < Scalers.Length; i++)
            {
                if(Scalers[i] != null) value = Scalers[i].Apply(value, in ctx);
            }
            return value;
        }
    }
}