using UnityEngine;

namespace CG.CombatFramework
{
    public readonly struct HitData
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Vector3 Direction;
        public readonly Hitbox Hitbox;
        public readonly float Distance;
        
        public HitData(Vector3 point, Vector3 normal, Vector3 direction, Hitbox hitbox = null, float distance = 0f)
        {
            Point = point;
            Normal = normal;
            Direction = direction;
            Hitbox = hitbox;
            Distance = distance;
        }

        public float Multiplier => Hitbox != null ? Hitbox.DamageMultiplier : 1f;

        public HitData AtDistance(float distance) => new(Point, Normal, Direction, Hitbox, distance);
    }
}