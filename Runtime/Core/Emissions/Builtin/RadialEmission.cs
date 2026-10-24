using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Radial (ring, fan)")]
    public sealed class RadialEmission : IEmission
    {
        [SerializeField, Min(1), Tooltip("How many total will go off spread over the arc")]
        private int count = 8;
        [SerializeField, Range(0f, 360f), Tooltip("360 = full ring, less = fan centered on aim")]
        private float arc = 360f;
        [SerializeField, Tooltip("Degrees to add to every direction (for turning whole pattern)")]
        private float offset;
        [SerializeReference, SelectType] private IEmission emission;

        public void Emit(in DeliveryContext ctx)
        {
            if (emission == null) return;
            bool ring = arc >= 359.9f;
            float reach = Vector3.Distance(ctx.Origin, ctx.AimPoint);
            for (int i = 0; i < count; i++)
            {
                float t = ring ? (float)i / count : count == 1 ? 0.5f : (float)i / (count - 1); //divide the count over the arc
                float angle = offset + (ring ? t * 360f : (t - 0.5f) * arc);
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * ctx.Direction;
                emission.Emit(new DeliveryContext(in ctx.Effect, ctx.Origin, dir, ctx.Origin + dir * reach));
            }
        }
    }
}
