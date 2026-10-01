using System.Collections.Generic;

namespace CG.CombatFramework
{
    public static class Conditions
    {
        public static bool All(this IReadOnlyList<ICondition> conditions, in EffectContext ctx)
        {
            if(conditions == null) return true;
            for(int i = 0; i < conditions.Count; i++)
            {
                if(conditions[i] != null && !conditions[i].Evaluate(in ctx)) return false;
            }
            return true;
        }

        public static bool Any(this IReadOnlyList<ICondition> conditions, in EffectContext ctx)
        {
            if(conditions == null || conditions.Count == 0) return true;
            for(int i = 0; i < conditions.Count; i++)
            {
                if(conditions[i] != null && conditions[i].Evaluate(in ctx)) return true;
            }
            return false;
        }
    }
}