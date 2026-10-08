using UnityEngine;

namespace CG.CombatFramework
{
    public readonly struct ProjectileFlight
    {
        public readonly float Speed;
        public readonly float Lifetime;
        public readonly float Radius;
        public readonly LayerMask Mask;
        public readonly QueryTriggerInteraction Triggers;
        public readonly int Pierce;
        public readonly IEmission OnHitUnit;
        public readonly IEmission OnStop;

        public ProjectileFlight(float speed, float lifetime, float radius, LayerMask mask, QueryTriggerInteraction triggers,
            int pierce, IEmission onHitUnit, IEmission onStop)
        {
            Speed = speed;
            Lifetime = lifetime;
            Radius = radius;
            Mask = mask;
            Triggers = triggers;
            Pierce = pierce;
            OnHitUnit = onHitUnit;
            OnStop = onStop;
        }
    }
}