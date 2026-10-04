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
            return 1f;
        }
    }
}