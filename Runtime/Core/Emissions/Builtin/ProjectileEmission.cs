using Unity.Plastic.Antlr3.Runtime.Tree;
using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Projectile")]
    public sealed class ProjectileEmission : IEmission
    {
        [Header("Emission Settings")]
        [SerializeField] private Projectile prefab;
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField, Range(0f, 45f)] private float spreadDegrees = 0f;
        [SerializeField, Tooltip("Whether vertical spread can be added. False for top-down")] private bool horizontalSpreadOnly = true;

        [Header("Flight Settings")]
        [SerializeField, Min(0f)] private float speed = 25f;
        [SerializeField, Min(0.01f)] private float lifetime = 2f;
        [SerializeField, Min(0f)] private float radius = 0f;
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide;
        [SerializeField, Min(0), Tooltip("How many extra units it can pass through after first")] private int pierce = 0;

        [Header("Arc")]
        [SerializeField, Min(0f), Tooltip("Downward pull in m/s^2. 0 = straight line")] 
        private float gravity = 0f;
        [SerializeField, Tooltip("Launch at the angle that lands on the aim bound (gravity must be above 0 and long enough lifeTime)")]
        private bool arcToAimPoint = false;

        [Header("Homing")]
        [SerializeField, Tooltip("Degrees per second it turns toward target. 0 = no homing")] private float homingTurnRate = 0f;
        [SerializeField, Tooltip("How far it looks for hostile when no target")] private float homingRadius = 15f;
        [SerializeField, Tooltip("Aim this far above target's pivot")] private float homingHeight = 1f;

        [Header("Bounc")]
        [SerializeField, Min(0), Tooltip("Will ricochet off level geometry this many times")] 
        private int bounces = 0;
        [SerializeField, Range(0f, 1f), Tooltip("As %")] private float bounceSpeedKept = 0.8f;

        [Header("Impact")]
        [SerializeReference, SelectType, Tooltip("Runs on each unit hit, usuallty a direct target query delivery")] 
        private IEmission onHitUnit;
        [SerializeReference, SelectType, Tooltip("Runs where it stops for any reason (like a wall, or end of lifetime)")] 
        private IEmission onStop;
        [SerializeField, Tooltip("When pierce runs out on a unit, run OnStop")]
        private bool stopAfterLastUnit = true;

        public void Emit(in DeliveryContext ctx)
        {
            if(prefab == null) return;

            var flight = new ProjectileFlight(speed, lifetime, radius, mask, triggers, pierce, onHitUnit, onStop, stopAfterLastUnit,
                gravity, arcToAimPoint, homingTurnRate, homingRadius, homingHeight, bounces, bounceSpeedKept);
            for(int i = 0; i < count; i++)
            {
                Vector3 dir = AimMath.Spread(ctx.Direction, spreadDegrees, horizontalSpreadOnly, ctx.Effect.Random);
                var shot = new DeliveryContext(in ctx.Effect, ctx.Origin, dir, ctx.AimPoint);
                ProjectilePool.Get(prefab).Launch(in shot, in flight);
            }
            
        }
    }
}