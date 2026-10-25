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
        public readonly IEmission[] OnHitUnit;
        public readonly IEmission[] OnStop;
        public readonly bool StopAfterLastUnit;
        public readonly float Gravity;
        public readonly bool ArcToAimPoint;
        public readonly float HomingTurnRate;
        public readonly float HomingRadius;
        public readonly float HomingHeight;
        public readonly int Bounces;
        public readonly float BounceSpeedKept;

        public ProjectileFlight(float speed, float lifetime, float radius, LayerMask mask, QueryTriggerInteraction triggers,
            int pierce, IEmission[] onHitUnit, IEmission[] onStop, bool stopAfterLastUnit, float gravity, bool arcToAimPoint,
            float homingTurnRate, float homingRadius, float homingHeight, int bounces, float bounceSpeedKept)
        {
            Speed = speed;
            Lifetime = lifetime;
            Radius = radius;
            Mask = mask;
            Triggers = triggers;
            Pierce = pierce;
            OnHitUnit = onHitUnit;
            OnStop = onStop;
            StopAfterLastUnit = stopAfterLastUnit;
            Gravity = gravity;
            ArcToAimPoint = arcToAimPoint;
            HomingTurnRate = homingTurnRate;
            HomingRadius = homingRadius;
            HomingHeight = homingHeight;
            Bounces = bounces;
            BounceSpeedKept = bounceSpeedKept;
        }
    }
}
