using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Relation (hostile, friendly, self)")]
    public sealed class RelationCondition : ICondition
    {
        [SerializeField] private bool hostile = true;
        [SerializeField] private bool neutral;
        [SerializeField] private bool friendly;

        [SerializeField, Tooltip("Source unit istel, or anything it owns")] private bool self;
        [SerializeField] private bool unknown;


        public bool Evaluate(in EffectContext ctx)
        {
            var source = ctx.SourceUnit;
            var target = ctx.TargetUnit;
            if (source == null || target == null) return unknown;
            if (ReferenceEquals(source.Root(), target.Root())) return self;

            switch (source.RelationTo(target))
            {
                case FactionRelation.Hostile: return hostile;
                case FactionRelation.Neutral: return neutral;
                case FactionRelation.Friendly: return friendly;
                default: return unknown;
            }
        }
    }
}