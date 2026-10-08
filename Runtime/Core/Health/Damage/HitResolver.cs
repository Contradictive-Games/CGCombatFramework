using UnityEngine;

namespace CG.CombatFramework
{
    public static class HitResolver
    {
        public static bool TryResolve(Collider coll, out IUnit unit, out Hitbox hitbox)
        {
            if(coll.TryGetComponent(out hitbox) && hitbox.Owner != null)
            {
                unit = hitbox.Owner;
                return true;
            }
            hitbox = null;
            Unit found = coll.GetComponentInParent<Unit>();
            unit = found != null ? found : null;
            return unit != null;
        }

        public static Vector3 ClosestPoint(Collider coll, Vector3 from)
        {
            return coll is MeshCollider { convex: false } ? coll.ClosestPointOnBounds(from) : coll.ClosestPoint(from);
        }
    }
}