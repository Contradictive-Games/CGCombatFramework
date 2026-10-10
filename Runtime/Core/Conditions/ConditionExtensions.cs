namespace CG.CombatFramework
{
    public static class Conditions
    {
        public static bool All(this ICondition[] conditions, in EffectContext ctx)
        {
            if(conditions == null) return true;
            for(int i = 0; i < conditions.Length; i++)
            {
                if(conditions[i] != null && !conditions[i].Evaluate(in ctx)) return false;
            }
            return true;
        }

        public static bool Any(this ICondition[] conditions, in EffectContext ctx)
        {
            if(conditions == null || conditions.Length == 0) return true;
            for(int i = 0; i < conditions.Length; i++)
            {
                if(conditions[i] != null && conditions[i].Evaluate(in ctx)) return true;
            }
            return false;
        }
    }
}