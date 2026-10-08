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

        [Header("Impact")]
        [SerializeReference, SelectType, Tooltip("Runs on each unit hit, usuallty a direct target query delivery")] 
        private IEmission onHitUnit;
        [SerializeReference, SelectType, Tooltip("Runs where it stops for any reason (like a wall, end of lifetime, or hit unit)")] 
        private IEmission onStop;

        public void Emit(in DeliveryContext ctx)
        {
            if(prefab == null) return;

            var flight = new ProjectileFlight(speed, lifetime, radius, mask, triggers, pierce, onHitUnit, onStop);
            for(int i = 0; i < count; i++)
            {
                Vector3 dir = AimMath.Spread(ctx.Direction, spreadDegrees, horizontalSpreadOnly, ctx.Effect.Random);
                var shot = new DeliveryContext(in ctx.Effect, ctx.Origin, dir, ctx.AimPoint);
                ProjectilePool.Get(prefab).Launch(in shot, in flight);
            }
            
        }
    }
}