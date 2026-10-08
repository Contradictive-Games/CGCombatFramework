using UnityEngine;

namespace CG.CombatFramework
{
    public static class AimMath
    {
        public static Vector3 Spread(Vector3 direction, float degrees, bool horizontalSpreadOnly, IGameRandom random)
        {
            if(degrees <= 0f || direction.sqrMagnitude < 0.0001f) return direction;
            float yaw = random.Range(-degrees, degrees);
            float pitch = horizontalSpreadOnly ? 0f : random.Range(-degrees, degrees);
            return Quaternion.LookRotation(direction) * Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        }
    }
}