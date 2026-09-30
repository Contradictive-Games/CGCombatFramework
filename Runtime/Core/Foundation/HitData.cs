using UnityEngine;

namespace CG.CombatFramework
{
    public readonly struct HitData
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Vector3 Direction;
        
        public HitData(Vector3 point, Vector3 normal, Vector3 direction)
        {
            Point = point;
            Normal = normal;
            Direction = direction;
        }
    }
}