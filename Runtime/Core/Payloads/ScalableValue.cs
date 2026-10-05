namespace CG.CombatFramework
{
    [System.Serializable]
    public sealed class ScalableValue
    {
        public float Base;
        [UnityEngine.SerializeReference, SelectType] 
        public System.Collections.Generic.List<IValueScaler> Scalers = new();

        public ScalableValue()
        {
            
        }

        public ScalableValue(float baseValue)
        {
            Base = baseValue;
        }

        public float Evalute(in EffectContext ctx)
        {
            float value = Base;
            for(int i = 0; i < Scalers.Count; i++)
            {
                if(Scalers[i] != null) value = Scalers[i].Apply(value, in ctx);
            }
            return value;
        }
    }
}