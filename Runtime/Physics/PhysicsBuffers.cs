using UnityEngine;

namespace CG.CombatFramework
{
    internal static class PhysicsBuffers
    {
        public static readonly Collider[] Colliders = new Collider[256];
        public static readonly RaycastHit[] Hits = new RaycastHit[64];
    }
}
