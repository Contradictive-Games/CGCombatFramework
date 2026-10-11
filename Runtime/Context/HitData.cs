using UnityEngine;

namespace CG.CombatFramework
{
    public readonly struct HitData
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Vector3 Direction;
        public readonly Hitbox Hitbox;
        
        public HitData(Vector3 point, Vector3 normal, Vector3 direction, Hitbox hitbox = null)
        {
            Point = point;
            Normal = normal;
            Direction = direction;
            Hitbox = hitbox;
        }

        public float Multiplier => Hitbox != null ? Hitbox.DamageMultiplier : 1f;
    }
}